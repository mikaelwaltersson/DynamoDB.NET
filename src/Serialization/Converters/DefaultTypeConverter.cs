using System.Collections.Concurrent;
using Amazon.DynamoDBv2.Model;

namespace DynamoDB.Net.Serialization.Converters;

class DefaultDynamoDBTypeConverter 
    : DynamoDBTypeConverter,
    IConvertFromNull, IConvertFromBoolean, 
    IConvertFromString, IConvertFromNumber, IConvertFromBinary,
    IConvertFromStringSet, IConvertFromNumberSet, IConvertFromBinarySet,
    IConvertFromList, 
    IConvertFromMap,
    IConvertToDynamoDBValue
{
    readonly ConcurrentDictionary<Type, SerializeToDynamoDBValueDelegate> serializeToDynamoDBValue = [];
    readonly ConcurrentDictionary<Type, DeserializeFromDynamoDBMapDelegate> deserializeFromDynamoDBMap = [];

    public override bool Handle(Type type) => 
        true;
    
    public object? ConvertFromNull(Type toType) => 
        TypeAssert.ForNull(toType);

    public object ConvertFromBoolean(bool value, Type toType) => 
        TypeAssert.ForValue(value, toType.UnwrapNullableType());

    public object ConvertFromString(string value, Type toType)
    {
        var parsableTypeConverter = GenericTypeInfo.Get(toType.UnwrapNullableType()).ParsableTypeConverter;

        return parsableTypeConverter != null
            ? parsableTypeConverter.ConvertFromString(value, toType)
            : TypeAssert.ForValue(value, toType);
    }

    public object ConvertFromNumber(string value, Type toType) => 
        DynamoDBNumber.StringToNumber(value, toType.UnwrapNullableType());

    public object ConvertFromBinary(MemoryStream value, Type toType) => 
        TypeAssert.ForValue(value.ToArray(), toType);

    public object ConvertFromStringSet(ICollection<string> values, Type toType, IConvertFromString convertFromString) => 
        DynamoDBSet.CreateSet(values, toType, convertFromString.ConvertFromString);

    public object ConvertFromNumberSet(ICollection<string> values, Type toType, IConvertFromNumber convertFromNumber) => 
        DynamoDBSet.CreateSet(values, toType, convertFromNumber.ConvertFromNumber);

    public object ConvertFromBinarySet(ICollection<MemoryStream> values, Type toType, IConvertFromBinary convertFromBinary) => 
        DynamoDBSet.CreateSet(values, toType, convertFromBinary.ConvertFromBinary);

    public object ConvertFromList(List<AttributeValue> elements, Type toType, IDynamoDBSerializer serializer) => 
        DynamoDBList.CreateList(elements, toType, serializer);

    public object ConvertFromMap(Dictionary<string, AttributeValue> entries, Type toType, IDynamoDBSerializer serializer) =>
        this.deserializeFromDynamoDBMap.GetOrAdd(toType, GetDeserializeFromDynamoDBMapDelegate)(entries, serializer);

    public AttributeValue ConvertToDynamoDBValue(object? value, Type fromType, IDynamoDBSerializer serializer) =>
        value == null 
            ? ConvertToNull()
            : this.serializeToDynamoDBValue.GetOrAdd(value.GetType(), GetSerializeToDynamoDBValueDelegate)(value, fromType, serializer);

    internal static bool IsSerializedAsPlainObject(Type type)
    {
        type = type.UnwrapNullableType();

        if (type == typeof(bool))
            return false;

        if (type == typeof(string))
            return false;

        if (DynamoDBNumber.IsSupportedType(type))
            return false;

        if (type == typeof(byte[]))
            return false;

        var typeInfo = GenericTypeInfo.Get(type);

        if (DynamoDBSet.IsSupportedType(type, typeInfo, out var _))
            return false;

        if (DynamoDBList.IsSupportedType(type, typeInfo, out var _))
            return false;

        if (typeInfo.IsPrimaryKeyType)
            return false;

        if (typeInfo.IsParsableType)
            return false;
        
        if (typeInfo.IsDictionaryType)
            return false;

        return Activator.IsConstructable(type);
    }

    internal static bool IsSerializedAttributeValue(DynamoDBAttributeInfo attributeInfo, Type propertyType, object? sourceValue) =>    
         attributeInfo.IsPrimaryKey || (
            (attributeInfo.SerializeNullValues || sourceValue != null) &&
            (attributeInfo.SerializeDefaultValues || !propertyType.IsDefaultValueTypeValue(sourceValue)));
    
    SerializeToDynamoDBValueDelegate GetSerializeToDynamoDBValueDelegate(Type type)
    {
        if (type == typeof(bool))
            return ConvertBooleanToDynamoDBValue;

        if (type == typeof(string))
            return ConvertStringToDynamoDBValue;

        if (DynamoDBNumber.IsSupportedType(type))
            return ConvertNumberToDynamoDBValue;

        if (type == typeof(byte[]))
            return ConvertByteArrayToDynamoDBValue;

        var typeInfo = GenericTypeInfo.Get(type);

        if (DynamoDBSet.IsSupportedType(type, typeInfo, out var setElementType))
        {
            if (Nullable.GetUnderlyingType(setElementType) != null)
                throw new DynamoDBSerializationException($"Nullable element type not supported for set type: {type}"); 

            if (setElementType == typeof(string))
                return ConvertStringSetToDynamoDBValue;

            if (DynamoDBNumber.IsSupportedType(setElementType))
                return Serializer.Create(typeof(NumberSetSerializer<>), setElementType);

            if (setElementType == typeof(byte[]))
                return ConvertByteArraySetToDynamoDBValue;

            return Serializer.Create(typeof(GenericSetSerializer<>), setElementType);
        }

        if (DynamoDBList.IsSupportedType(type, typeInfo, out var listElementType))
            return Serializer.Create(typeof(ListSerializer<>), listElementType);

        if (typeInfo.PrimaryKeyItemType != null)
            return Serializer.Create(typeof(PrimaryKeySerializer<>), typeInfo.PrimaryKeyItemType);

        if (typeInfo.ParsableTypeConverter is not null)
            return typeInfo.ParsableTypeConverter.ConvertToDynamoDBValue;

        if (!Activator.IsConstructable(type))
             throw new DynamoDBSerializationException($"Type not supported: {type}");
        
        if (typeInfo.DictionaryTypes is (var keyType, var valueType))
            return Serializer.Create(typeof(DictionarySerializer<,>), keyType, valueType);

        return Serializer.Create(typeof(PlainObjectSerializer<>), type);
    }

    DeserializeFromDynamoDBMapDelegate GetDeserializeFromDynamoDBMapDelegate(Type type)
    {
        var typeInfo = GenericTypeInfo.Get(type);

        if (typeInfo.PrimaryKeyItemType != null)
            return Deserializer.Create(typeof(PrimaryKeyDeserializer<>), typeInfo.PrimaryKeyItemType);

        if (typeInfo.DictionaryTypes is (var keyType, var valueType))
        {
            if (type == typeof(IDictionary<,>).MakeGenericType(keyType, valueType))
                type = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);

            if (Activator.IsConstructable(type))
                return Deserializer.Create(typeof(DictionaryDeserializer<,,>), type, keyType, valueType);
        }
        else if (IsSerializedAsPlainObject(type))
            return PlainObjectDeserializer.ForObjectType(type).FromDynamoDBMap;

        throw new DynamoDBSerializationException($"Type can not be deserialized from map: {type}");
    }

    static AttributeValue ConvertToNull() => 
        new() { NULL = true };

    static AttributeValue ConvertBooleanToDynamoDBValue(object value, Type fromType, IDynamoDBSerializer serializer) =>
        new() { BOOL = (bool)value };

    static AttributeValue ConvertStringToDynamoDBValue(object value, Type fromType, IDynamoDBSerializer serializer) =>
        new() { S = ((string)value) };

    static AttributeValue ConvertNumberToDynamoDBValue(object value, Type fromType, IDynamoDBSerializer serializer) =>
        new() { N = value.ToString() };

    static AttributeValue ConvertByteArrayToDynamoDBValue(object value, Type fromType, IDynamoDBSerializer serializer) =>
        new() { B =  new((byte[])value) };

    static AttributeValue ConvertStringSetToDynamoDBValue(object value, Type fromType, IDynamoDBSerializer serializer) =>
        new() { SS = new(((ISet<string>)value).Select(AssertSetElementIsNotNull)) };
            
    static AttributeValue ConvertByteArraySetToDynamoDBValue(object value, Type fromType, IDynamoDBSerializer serializer) =>
        new() { BS = new(((ISet<byte[]>)value).Select(AssertSetElementIsNotNull).Select(static element => new MemoryStream(element))) };

    static DynamoDBSerializationException MissingRequiredAttribute(Type type, string attributeName) => 
        new($"Missing required attribute '{attributeName}' when deserializing '{type.FullName}'");

    static T AssertSetElementIsNotNull<T>(T? element) where T : notnull =>
        element ?? throw new DynamoDBSerializationException($"Failed to serialize value of type: {typeof(ISet<T>)} (contains null values)");
    
    delegate AttributeValue SerializeToDynamoDBValueDelegate(object value, Type fromType, IDynamoDBSerializer serializer);

    delegate object DeserializeFromDynamoDBMapDelegate(Dictionary<string, AttributeValue> entries, IDynamoDBSerializer serializer);

    abstract class Serializer
    {
        public static SerializeToDynamoDBValueDelegate Create(Type genericTypeDefinition, params Type[] typeArguments) =>
            ((Serializer)Activator.CreateInstance(genericTypeDefinition.MakeGenericType(typeArguments))).ToDynamoDBValue;

        public abstract AttributeValue ToDynamoDBValue(object value, Type fromType, IDynamoDBSerializer serializer);
    }

    class NumberSetSerializer<T> : Serializer where T : notnull
    {
        public override AttributeValue ToDynamoDBValue(object value, Type fromType, IDynamoDBSerializer serializer) =>
            new() { NS = new(((ISet<T>)value).Select(static element => element.ToString())) };
    }

    class GenericSetSerializer<T> : Serializer
    {
        public override AttributeValue ToDynamoDBValue(object value, Type fromType, IDynamoDBSerializer serializer) =>
            ToDynamoDBSet(((ISet<T>)value).Select(element => serializer.SerializeDynamoDBValue(element, typeof(T))));
                
        static AttributeValue ToDynamoDBSet(IEnumerable<AttributeValue> elements)
        {
            var serializedSet = new AttributeValue();

            foreach (var element in elements)
            {
                switch ((element, serializedSet))
                {
                    case ({ S: not null }, { NS.Count: 0, BS.Count: 0 }):
                        serializedSet.SS.Add(element.S);
                        break;

                    case ({ N: not null }, { SS.Count: 0, BS.Count: 0 }):
                        serializedSet.NS.Add(element.N);
                        break;

                    case ({ B: not null }, { SS.Count: 0, NS.Count: 0 }):
                        serializedSet.BS.Add(element.B);
                        break;

                    default:
                        throw new DynamoDBSerializationException($"Failed to serialize value of type: {typeof(ISet<T>)} (contains null values or values serialized to mixed/invalid DynamoDB types)");
                }
            }

            return serializedSet;
        }
    }

    class ListSerializer<T> : Serializer
    {
        public override AttributeValue ToDynamoDBValue(object value, Type fromType, IDynamoDBSerializer serializer) =>
            new() 
            { 
                L = new(((IList<T>)value).Select(element => serializer.SerializeDynamoDBValue(element, typeof(T)))), 
                IsLSet = true 
            };
    }

    class PrimaryKeySerializer<T> : Serializer where T : class
    {
        public override AttributeValue ToDynamoDBValue(object value, Type fromType, IDynamoDBSerializer serializer)
        {
            var keyValue = (PrimaryKey<T>)value;
            var serializedKeyValue = new AttributeValue();
        
            if (keyValue != default)
            {
                var partitionKeyInfo = serializer.GetPropertyAttributeInfo(TableDescription.Properties<T>.PartitionKey);
            
                serializedKeyValue.M[partitionKeyInfo.AttributeName] = serializer.SerializeDynamoDBValue(keyValue.PartitionKey, TableDescription.PropertyTypes<T>.PartitionKey);

                if (TableDescription.Properties<T>.SortKey != null)
                {
                    var sortKeyInfo = serializer.GetPropertyAttributeInfo(TableDescription.Properties<T>.SortKey.Value);

                    serializedKeyValue.M[sortKeyInfo.AttributeName] = serializer.SerializeDynamoDBValue(keyValue.SortKey, TableDescription.PropertyTypes<T>.SortKey!);
                }
            }

            return serializedKeyValue;
        }
    }

    class DictionarySerializer<TKey, TValue> : Serializer where TKey : notnull
    {
        public override AttributeValue ToDynamoDBValue(object value, Type fromType, IDynamoDBSerializer serializer) =>
            new() 
            { 
                M = new(
                    ((IDictionary<TKey, TValue>)value)
                        .Select(entry => 
                            KeyValuePair.Create(
                                SerializeKey(entry.Key, serializer), 
                                serializer.SerializeDynamoDBValue(entry.Value, typeof(TValue))))), 
                IsMSet = true
            };

        static string SerializeKey(TKey key, IDynamoDBSerializer serializer)
        {
            var serializedKeyValue = serializer.SerializeDynamoDBValue(key, typeof(TKey));

            return 
                serializedKeyValue.S ?? 
                serializedKeyValue.N ?? 
                serializedKeyValue.B?.ToBase64String() ?? 
                throw new DynamoDBSerializationException($"Invalid key type for dictionary: {typeof(TKey)}'");
        }
    }

    class PlainObjectSerializer<T> : Serializer where T : notnull
    {
        static readonly List<(string Name, Type Type, Func<T, object?> GetValue)> properties = 
            typeof(T).GetSerializablePropertiesAndFields()
                .Select(property => (property.Name, property.GetPropertyType(), property.CompilePropertyGetter<T, object?>()))
                .ToList();
    
        public override AttributeValue ToDynamoDBValue(object value, Type fromType, IDynamoDBSerializer serializer)
        {
            var sourceObject = (T)value;

            var attributeValues =
                from property in properties
                let attributeInfo = serializer.GetPropertyAttributeInfo((typeof(T), property.Name))
                where !attributeInfo.NotSerialized
                let sourceValue = attributeInfo.GetPropertyValue(sourceObject, property.Name, property.GetValue)
                where IsSerializedAttributeValue(attributeInfo, property.Type, sourceValue)
                let serializedValue = serializer.SerializeDynamoDBValue(sourceValue, property.Type)
                where !serializedValue.IsEmpty()
                select KeyValuePair.Create(attributeInfo.AttributeName, serializedValue);

            if (fromType != typeof(T))
            {
                var typeDiscriminator =
                    KeyValuePair.Create(
                        serializer.ObjectTypeNameResolver.Attribute,
                        serializer.SerializeDynamoDBValue(serializer.ObjectTypeNameResolver.GetTypeName(typeof(T))));

                attributeValues = attributeValues.Prepend(typeDiscriminator);
            }

            return
                new()
                {
                    M = new(attributeValues),
                    IsMSet = true,
                };
        }
    }

    abstract class Deserializer
    {
        protected static Deserializer UntypedDictionaryDeserializer { get; } =  
            new DictionaryDeserializer<Dictionary<string, object>, string, object>();

        public static DeserializeFromDynamoDBMapDelegate Create(Type genericTypeDefinition, params Type[] typeArguments) =>
            ((Deserializer)Activator.CreateInstance(genericTypeDefinition.MakeGenericType(typeArguments))).FromDynamoDBMap;

        public abstract object FromDynamoDBMap(Dictionary<string, AttributeValue> entries, IDynamoDBSerializer serializer);
    }

    class PrimaryKeyDeserializer<T> : Deserializer where T : class
    {
        public override object FromDynamoDBMap(Dictionary<string, AttributeValue> entries, IDynamoDBSerializer serializer)
        {
            var partitionKeyInfo = serializer.GetPropertyAttributeInfo(TableDescription.Properties<T>.PartitionKey);
            if (!entries.TryGetValue(partitionKeyInfo.AttributeName, out var partitionKeyValue))
                throw MissingRequiredAttribute(typeof(PrimaryKey<T>), partitionKeyInfo.AttributeName);

            var partitionKey = serializer.DeserializeDynamoDBValue(partitionKeyValue, TableDescription.PropertyTypes<T>.PartitionKey, pathElement: partitionKeyInfo.AttributeName);
            var sortKey = default(object?);

            if (TableDescription.Properties<T>.SortKey != null)
            {
                var sortKeyInfo = serializer.GetPropertyAttributeInfo(TableDescription.Properties<T>.SortKey.Value);
                if (!entries.TryGetValue(sortKeyInfo.AttributeName, out var sortKeyInfoValue))
                    throw MissingRequiredAttribute(typeof(PrimaryKey<T>), sortKeyInfo.AttributeName);

                sortKey = serializer.DeserializeDynamoDBValue(sortKeyInfoValue, TableDescription.PropertyTypes<T>.SortKey!, pathElement: partitionKeyInfo.AttributeName);   
            }

            return PrimaryKey<T>.FromTuple((partitionKey, sortKey));  
        }
    }

    class DictionaryDeserializer<TDictionary, TKey, TValue> : Deserializer
        where TDictionary : IDictionary<TKey, TValue>, new()
        where TKey : notnull
    {
        public override object FromDynamoDBMap(Dictionary<string, AttributeValue> entries, IDynamoDBSerializer serializer)
        {
            var dictionary = new TDictionary();

            foreach (var entry in entries)
                dictionary.Add(DeserializeKey(entry.Key, serializer), serializer.DeserializeDynamoDBValue<TValue>(entry.Value, pathElement: entry.Key)!);

            return dictionary;
        }

        TKey DeserializeKey(string key, IDynamoDBSerializer serializer) =>
            serializer.DeserializeDynamoDBValue<TKey>(PreDeserializeFormatKey(key), pathElement: key)!;

        Func<string, AttributeValue> PreDeserializeFormatKey { get; } =
            typeof(TKey) == typeof(byte[])
                ? (string key) => new AttributeValue { B = new(Convert.FromBase64String(key)) }
                : DynamoDBNumber.IsSupportedType(typeof(TKey))
                    ? (string key) => new AttributeValue { N = key }
                    : (string key) => new AttributeValue { S = key };
    }

    abstract class PlainObjectDeserializer : Deserializer
    {
        static readonly ConcurrentDictionary<Type, PlainObjectDeserializer> instances = [];

        public static PlainObjectDeserializer ForObjectType(Type objectType) =>
            instances.GetOrAdd(objectType, static type => (PlainObjectDeserializer)Activator.CreateInstance(typeof(PlainObjectDeserializer<>).MakeGenericType(type)));
    
        public abstract object DeserializeConcreteObject(Dictionary<string, AttributeValue> entries, IDynamoDBSerializer serializer);
    }

    class PlainObjectDeserializer<T> : PlainObjectDeserializer where T : notnull
    {
        readonly List<(string Name, Type Type, Action<T, object?> SetValue)> properties = 
            typeof(T).GetSerializablePropertiesAndFields()
                .Select(property => (property.Name, property.GetPropertyType(), property.CompilePropertySetter<T, object?>()))
                .ToList();

        public override object FromDynamoDBMap(Dictionary<string, AttributeValue> entries, IDynamoDBSerializer serializer)
        {
            var deserialize = DeserializeConcreteObject;
   
            if (entries.TryGetValue(serializer.ObjectTypeNameResolver.Attribute, out var typeDiscriminator))
            {
                if (typeDiscriminator is not { S: var typeName })
                    throw new DynamoDBSerializationException($"Type discriminator '{serializer.ObjectTypeNameResolver.Attribute}' expected to be of type S");
                
                var type = serializer.ObjectTypeNameResolver.GetObjectType(typeName);

                deserialize = ForObjectType(type).DeserializeConcreteObject;
            }
            else if (typeof(T) == typeof(object))
            {
                deserialize = UntypedDictionaryDeserializer.FromDynamoDBMap;
            }

            return deserialize(entries, serializer);
        }

        public override object DeserializeConcreteObject(Dictionary<string, AttributeValue> entries, IDynamoDBSerializer serializer)
        {
            var targetObject = Activator.CreateInstance<T>();

            var targetPropertyToAttributeMapping = 
                from property in properties
                let attributeInfo = serializer.GetPropertyAttributeInfo((typeof(T), property.Name))
                where !attributeInfo.NotSerialized
                select (property, attributeInfo);
            
            foreach (var (property, attributeInfo) in targetPropertyToAttributeMapping)
            {
                if (entries.TryGetValue(attributeInfo.AttributeName, out var value))
                {
                    var deserializedValue = serializer.DeserializeDynamoDBValue(value, property.Type, pathElement: attributeInfo.AttributeName);
                    
                    attributeInfo.SetPropertyValue(targetObject, property.Name, deserializedValue, property.SetValue);
                }
            }

            return targetObject;
        }
    }

    static class TypeAssert
    {
        public static object? ForNull(Type toType)
        {
            if (toType.IsValueType && Nullable.GetUnderlyingType(toType) == null)
                throw new DynamoDBSerializationException($"Type can not be deserialized from null: {toType}");

            return null;
        }

        public static object ForValue<T>(T value, Type toType) where T : notnull
        {
            if (!toType.IsAssignableFrom(typeof(T)))
                throw new DynamoDBSerializationException($"Type can not be deserialized from {typeof(T).Name}: {toType.FullName}");

            return value;
        }
    }
}


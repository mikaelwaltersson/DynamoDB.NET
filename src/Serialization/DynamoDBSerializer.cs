using System.Collections.Concurrent;
using System.Reflection;
using Amazon.DynamoDBv2.Model;
using DynamoDB.Net.Model;
using DynamoDB.Net.Serialization.Converters;
using Microsoft.Extensions.Options;

namespace DynamoDB.Net.Serialization;

public sealed class DynamoDBSerializer(IOptions<DynamoDBSerializerOptions> options) : IDynamoDBSerializer
{
    readonly ConcurrentDictionary<Type, Dictionary<string, DynamoDBAttributeInfo>> cachedAttributeInfoLookups = [];
    readonly NameTransform attributeNameTransform = options.Value.AttributeNameTransform;
    readonly ResolvedTypeConverter typeConverter = new([.. options.Value.TypeConverters]);
    readonly bool serializeDefaultValues = options.Value.SerializeDefaultValues;
    readonly bool serializeNullValues = options.Value.SerializeNullValues;
    readonly DynamoDBObjectTypeNameResolver objectTypeNameResolver = options.Value.ObjectTypeNameResolver;
    readonly IEnumerable<IOnDeserializeProperty> onDeserializeProperty = [.. options.Value.OnDeserializeProperty];
    readonly IEnumerable<IOnSerializeProperty> onSerializeProperty = [.. options.Value.OnSerializeProperty];
    
    public static readonly DynamoDBSerializer Default = new(Options.Create(new DynamoDBSerializerOptions()));

    public object? DeserializeDynamoDBValue(AttributeValue value, Type objectType) =>
        value switch
        {
            { NULL: true } => typeConverter.ConvertFromNull(objectType),

            { IsBOOLSet: true } => typeConverter.ConvertFromBoolean(value.BOOL, objectType),

            { S: not null } => typeConverter.ConvertFromString(value.S, objectType),

            { B: not null } => typeConverter.ConvertFromBinary(value.B, objectType),

            { N: not null } => typeConverter.ConvertFromNumber(value.N, objectType),

            { SS.Count: > 0 } => typeConverter.ConvertFromStringSet(value.SS, objectType),

            { NS.Count: > 0 } => typeConverter.ConvertFromNumberSet(value.NS, objectType),

            { BS.Count: > 0 } => typeConverter.ConvertFromBinarySet(value.BS, objectType),

            { IsLSet: true } => typeConverter.ConvertFromList(value.L, objectType, this),
            
            { IsMSet: true } => typeConverter.ConvertFromMap(value.M, objectType, this),
            
            _=> throw new ArgumentOutOfRangeException(nameof(value))
        };

    public AttributeValue SerializeDynamoDBValue(object? value, Type objectType) =>
        typeConverter.ConvertToDynamoDBValue(value, objectType, this);

    public DynamoDBAttributeInfo GetPropertyAttributeInfo((Type DeclaringType, string Name) property)
    {
        var attributeInfoLookup = 
            cachedAttributeInfoLookups.GetOrAdd(
                property.DeclaringType, 
                type => 
                {
                    if (!typeConverter.IsSerializedAsPlainObject(type))
                        throw new InvalidOperationException($"Can not access member '{property.Name}', type '{type.FullName}' is not serialized as a plain object");

                    var lookupEntries =
                        from property in type.GetSerializablePropertiesAndFields()
                        let attributeInfo = GetDynamoDBAttributeInfo(property)
                        select (property.Name, attributeInfo);

                    return lookupEntries.ToDictionary();
                });

        if (!attributeInfoLookup.TryGetValue(property.Name, out var attributeInfo))
            throw new DynamoDBSerializationException($"No serializable property '{property.Name}' on type '{property.DeclaringType.FullName}'");
        
        return attributeInfo;
    }

    public DynamoDBObjectTypeNameResolver ObjectTypeNameResolver => objectTypeNameResolver;

    DynamoDBAttributeInfo GetDynamoDBAttributeInfo(MemberInfo memberInfo)
    {
        var propertyAttribute = memberInfo.GetCustomAttribute<DynamoDBPropertyAttribute>();

        return new()
        {
            AttributeName = 
                propertyAttribute?.AttributeName ?? 
                attributeNameTransform.TransformName(memberInfo.Name),

            IsPrimaryKey = 
                memberInfo.HasCustomAttribute<IndexKeyAttribute>(attribute => attribute.IndexType == IndexType.PrimaryKey),

            SerializeDefaultValues =
                propertyAttribute is { SerializeDefaultValuesIsSpecified: true, SerializeDefaultValues: var serializeDefaultValueOverride }
                    ? serializeDefaultValueOverride 
                    : serializeDefaultValues,

            SerializeNullValues =
                propertyAttribute is { SerializeNullValuesIsSpecified: true, SerializeNullValues: var serializeNullValuesOverride }
                    ? serializeNullValuesOverride 
                    : serializeNullValues,

            NotSerialized = 
                propertyAttribute?.NotSerialized ?? 
                false,

            OnDeserializeProperty =
                memberInfo.GetCustomAttributes()
                    .OfType<IOnDeserializeProperty>()
                    .Concat(onDeserializeProperty),

            OnSerializeProperty =
                memberInfo.GetCustomAttributes()
                    .OfType<IOnSerializeProperty>()
                    .Concat(onSerializeProperty)
        };
    }

    class ResolvedTypeConverter(IEnumerable<DynamoDBTypeConverter> converters) 
        : IConvertFromString, IConvertFromNumber, IConvertFromBinary
    {
        readonly ConcurrentDictionary<(Type, Type), object> resolvedTypeConverters = [];

        T ResolveTypeConverter<T>(Type type) where T : class => 
            (T)resolvedTypeConverters.GetOrAdd(
                (type, typeof(T)), 
                ((Type Type, Type ConverterType) key) =>
                    converters
                        .Append(DynamoDBTypeConverter.Default)
                        .First(converter => 
                            key.ConverterType.IsInstanceOfType(converter) && 
                            converter.Handle(key.Type)));
    
        public AttributeValue ConvertToDynamoDBValue(object? value, Type fromType, IDynamoDBSerializer serializer) =>
            ResolveTypeConverter<IConvertToDynamoDBValue>(fromType).ConvertToDynamoDBValue(value, fromType, serializer);

        public object? ConvertFromNull(Type toType) => 
            ResolveTypeConverter<IConvertFromNull>(toType).ConvertFromNull(toType);

        public object ConvertFromBoolean(bool value, Type toType) => 
            ResolveTypeConverter<IConvertFromBoolean>(toType).ConvertFromBoolean(value, toType);

        public object ConvertFromString(string value, Type toType) => 
            ResolveTypeConverter<IConvertFromString>(toType).ConvertFromString(value, toType);

        public object ConvertFromNumber(string value, Type toType) => 
            ResolveTypeConverter<IConvertFromNumber>(toType).ConvertFromNumber(value, toType);

        public object ConvertFromBinary(MemoryStream value, Type toType) => 
            ResolveTypeConverter<IConvertFromBinary>(toType).ConvertFromBinary(value, toType);

        public object ConvertFromStringSet(ICollection<string> values, Type toType) => 
            ResolveTypeConverter<IConvertFromStringSet>(toType).ConvertFromStringSet(values, toType, this);

        public object ConvertFromNumberSet(ICollection<string> values, Type toType) => 
            ResolveTypeConverter<IConvertFromNumberSet>(toType).ConvertFromNumberSet(values, toType, this);

        public object ConvertFromBinarySet(ICollection<MemoryStream> values, Type toType) => 
            ResolveTypeConverter<IConvertFromBinarySet>(toType).ConvertFromBinarySet(values, toType, this);

        public object ConvertFromList(List<AttributeValue> elements, Type toType, IDynamoDBSerializer serializer) => 
            ResolveTypeConverter<IConvertFromList>(toType).ConvertFromList(elements, toType, serializer);

        public object ConvertFromMap(Dictionary<string, AttributeValue> entries, Type toType, DynamoDBSerializer serializer) =>
            ResolveTypeConverter<IConvertFromMap>(toType).ConvertFromMap(entries, toType, serializer);
    
        public bool IsSerializedAsPlainObject(Type type) =>
            ResolveTypeConverter<IConvertToDynamoDBValue>(type) == DynamoDBTypeConverter.Default &&
            DefaultDynamoDBTypeConverter.IsSerializedAsPlainObject(type);
    }
}

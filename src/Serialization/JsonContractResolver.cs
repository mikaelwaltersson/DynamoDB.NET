using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using Amazon.DynamoDBv2.Model;

using DynamoDB.Net.Model;
using DynamoDB.Net.Serialization.Converters;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;


namespace DynamoDB.Net.Serialization
{
    public class JsonContractResolver : DefaultContractResolver
    {
        static Dictionary<string, Predicate<AttributeValue>> attributeValueShouldSerializeLookup =
            new Dictionary<string, Predicate<AttributeValue>>(StringComparer.Ordinal)
            {
                ["B"] = target => target.B != null,
                ["BOOL"] = target => target.IsBOOLSet,
                ["BS"] = target => target.BS != null && target.BS.Count > 0,
                ["L"] = target => target.IsLSet,
                ["M"] = target => target.IsMSet,
                ["N"] = target => target.N != null,
                ["NS"] = target => target.NS != null && target.NS.Count > 0,
                ["NULL"] = target => target.NULL,
                ["S"] = target => target.S != null,
                ["SS"] = target => target.SS != null && target.SS.Count > 0
            };

        ConcurrentDictionary<Type, Model.TableDescription> tableDescriptions =
            new ConcurrentDictionary<Type, Model.TableDescription>();

        public static readonly JsonContractResolver Default = new JsonContractResolver();
        
        public static readonly JsonContractResolver DefaultDynamoDB = new JsonContractResolver(SerializationTarget.DynamoDB);



        public JsonContractResolver(SerializationTarget serializationTarget = SerializationTarget.Json)
        {
            SerializationTarget = serializationTarget;
            NamingStrategy = new CamelCaseNamingStrategy(processDictionaryKeys: true, overrideSpecifiedNames: false);
        }

        public SerializationTarget SerializationTarget { get; set; }

        public List<JsonConverter> Converters { get; set; } =
            new List<JsonConverter>
            {
                new MemoryStreamJsonConverter(),
                new ByteArrayJsonConverter(),
                new Iso8601JsonConverter(),
                new StringEnumConverter(camelCaseText: true),

                new DynamoDBSetJsonConverter(typeof(string)),
                new DynamoDBSetJsonConverter(typeof(char)),
                new DynamoDBSetJsonConverter(typeof(Uri), itemParser: s => new Uri(s)),
                new DynamoDBSetJsonConverter(new Iso8601JsonConverter()),
                new DynamoDBSetJsonConverter(new ByteArrayJsonConverter(), comparer: ByteArrayComparer.Default),
                
                new DynamoDBSetJsonConverter(typeof(byte)),
                new DynamoDBSetJsonConverter(typeof(sbyte)),
                new DynamoDBSetJsonConverter(typeof(ushort)),
                new DynamoDBSetJsonConverter(typeof(short)),
                new DynamoDBSetJsonConverter(typeof(uint)),
                new DynamoDBSetJsonConverter(typeof(int)),
                new DynamoDBSetJsonConverter(typeof(ulong)),
                new DynamoDBSetJsonConverter(typeof(long)),
                new DynamoDBSetJsonConverter(typeof(float)),
                new DynamoDBSetJsonConverter(typeof(double)),
                new DynamoDBSetJsonConverter(typeof(decimal)),
                new DynamoDBSetJsonConverter(new StringEnumConverter(camelCaseText: true)),
            };


        public List<IPropertyDecorator> PropertyDecorators { get; set; } =
            new List<IPropertyDecorator>
            {
                new SerializeDefaultValueForNonFlagsEnumsPropertyDecorator()
            };


        public Model.TableDescription GetTableDescription(Type type) =>
            tableDescriptions.GetOrAdd(type, key => Model.TableDescription.Get(key, this));


        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            var properties = 
                base.CreateProperties(type, memberSerialization).
                    OrderBy(p => GetPropertyClassHiearchyDepth(type, p)).
                    ThenBy(p => GetDeclaredPropertyOrder(p)).
                    ToList();

            if (type == typeof(AttributeValue))
                SetUpAttributeValueProperties(properties);
            else
                properties.ForEach(SetUpProperty);

            return properties;
        }

        protected override JsonConverter ResolveContractConverter(Type objectType) =>
            GetDefaultConverter(objectType) ??
            base.ResolveContractConverter(objectType);


        protected virtual void SetUpProperty(JsonProperty property)
        {
            var propertyDecorators = property.AttributeProvider.GetAttributes(true).OfType<IPropertyDecorator>();

            foreach (var decorator in this.PropertyDecorators.Concat(propertyDecorators))
                decorator.Decorate(property, this);  

            if (property.Ignored)
                return;

            if (GetDefaultConverter(property.PropertyType) is DynamoDBSetJsonConverter)
                SetUpDynamoDBSetProperty(property);
        }

        protected virtual JsonConverter GetDefaultConverter(Type objectType)
        {
            var primaryKeyUnderlyingType = PrimaryKey.GetUnderlyingType(objectType);
            if (primaryKeyUnderlyingType != null)
                return(JsonConverter)typeof(PrimaryKeyJsonConverter<>).MakeGenericType(primaryKeyUnderlyingType).CreateInstance(this);

            return Converters.FirstOrDefault(converter => converter.CanConvert(objectType));
        }

        void SetUpDynamoDBSetProperty(JsonProperty property)
        {
            var parameter = Expression.Parameter(typeof(object), "obj");
            var member =
                Expression.Convert(
                    Expression.PropertyOrField(
                        Expression.Convert(parameter, property.DeclaringType), property.UnderlyingName),
                    typeof(ICollection<>).MakeGenericType(property.PropertyType.GetGenericArguments()[0]));

            var body = 
                Expression.AndAlso(
                    Expression.NotEqual(member, Expression.Constant(null, member.Type)),
                    Expression.GreaterThan(Expression.PropertyOrField(member, nameof(ICollection<object>.Count)), Expression.Constant(0)));
                    
            property.SetIsSpecified = null;
            property.GetIsSpecified = Expression.Lambda<Predicate<object>>(body, parameter).Compile();
        }

        static void SetUpAttributeValueProperties(List<JsonProperty> properties)
        {
            foreach (var property in properties)
            {
                property.PropertyName = property.UnderlyingName;
                property.Ignored = !attributeValueShouldSerializeLookup.ContainsKey(property.PropertyName);

                if (!property.Ignored)
                    property.ShouldSerialize = DownCastPredicate(attributeValueShouldSerializeLookup[property.PropertyName]);
            }
        }



        int GetPropertyClassHiearchyDepth(Type type, JsonProperty property)
        {
            var depth = 0;

            for (var t = type; t != null; t = t.GetTypeInfo().BaseType)
                depth++;

            for (var t = type; t != null && t != property.DeclaringType; t = t.GetTypeInfo().BaseType)
                depth--;

            return depth;
        }

        int GetDeclaredPropertyOrder(JsonProperty property)
        {
            var propertiesForType = property.DeclaringType.GetProperties();
            var indexOfProperty = Array.FindIndex(propertiesForType, p => p.Name == property.UnderlyingName);
            return indexOfProperty < 0 ? indexOfProperty : propertiesForType[indexOfProperty].MetadataToken; // Undocumented but seems to work
        }
        
        static Predicate<object> DownCastPredicate<T>(Predicate<T> predicate) => target => predicate((T)target);
    }
}
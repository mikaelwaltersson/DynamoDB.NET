using System;

using Amazon.DynamoDBv2.Model;

using Newtonsoft.Json;

namespace DynamoDB.Net.Serialization
{
    public static class JsonSerializerExtensions
    {
        public static Model.TableDescription GetTableDescription(this JsonSerializer serializer, Type type) =>
            ((JsonContractResolver)serializer.ContractResolver).GetTableDescription(type);

        public static AttributeValue SerializeDynamoDBValue(this JsonSerializer serializer, object value, Type objectType, DynamoDBJsonWriterFlags flags = default(DynamoDBJsonWriterFlags))
        {
            var attributeValue = new AttributeValue();

            serializer.Serialize(new DynamoDBJsonWriter(attributeValue, flags), value, objectType);

            return attributeValue;
        }

        public static AttributeValue SerializeDynamoDBValue<T>(this JsonSerializer serializer, T value, DynamoDBJsonWriterFlags flags = default(DynamoDBJsonWriterFlags)) =>
            serializer.SerializeDynamoDBValue(value, typeof(T), flags);

        public static T DeserializeDynamoDBValue<T>(this JsonSerializer serializer, AttributeValue value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            return serializer.Deserialize<T>(new DynamoDBJsonReader(value));
        }

        public static object DeserializeDynamoDBValue(this JsonSerializer serializer, AttributeValue value, Type objectType)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            return serializer.Deserialize(new DynamoDBJsonReader(value), objectType);
        }
    }
}
using System;
using System.Collections;
using System.Reflection;
using Amazon.DynamoDBv2.Model;

namespace DynamoDB.Net.Serialization;

public interface IDynamoDBSerializer
{
    object DeserializeDynamoDBValue(AttributeValue value, Type objectType);

    T DeserializeDynamoDBValue<T>(AttributeValue value) => (T)DeserializeDynamoDBValue(value, typeof(T));

    AttributeValue SerializeDynamoDBValue(object value, Type objectType, SerializeDynamoDBValueTarget target = default);

    AttributeValue SerializeDynamoDBValue<T>(T value, SerializeDynamoDBValueTarget target = default) => SerializeDynamoDBValue(value, typeof(T), target);

    string GetSerializedPropertyName(MemberInfo property);

    bool TryCreateDynamoDBSet(Type elementType, IEnumerable values, out object dynamoDBSet);
}

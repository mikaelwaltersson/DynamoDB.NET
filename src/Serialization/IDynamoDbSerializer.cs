using System;
using System.Collections;
using System.Reflection;
using Amazon.DynamoDBv2.Model;

namespace DynamoDB.Net.Serialization;

public interface IDynamoDBSerializer
{
    object DeserializeDynamoDBValue(AttributeValue value, Type objectType);

    T DeserializeDynamoDBValue<T>(AttributeValue value) =>
        (T)DeserializeDynamoDBValue(value, typeof(T));

    AttributeValue SerializeDynamoDBValue(object value, Type objectType, SerializeDynamoDBValueFlags flags = default);

    AttributeValue SerializeDynamoDBValue<T>(T value, SerializeDynamoDBValueFlags flags = default) =>
        SerializeDynamoDBValue(value, typeof(T), flags);

    Model.TableDescription GetTableDescription(Type type);

    string GetSerializedPropertyName(MemberInfo memberInfo);

    bool TryCreateDynamoDBSet(Type elementType, IEnumerable values, out object dynamoDBSet);
}

using System.Reflection;
using Amazon.DynamoDBv2.Model;

namespace DynamoDB.Net.Serialization;

public interface IDynamoDBSerializer
{
    object? DeserializeDynamoDBValue(AttributeValue value, Type objectType);

    T? DeserializeDynamoDBValue<T>(AttributeValue value) => (T?)DeserializeDynamoDBValue(value, typeof(T));

    AttributeValue SerializeDynamoDBValue(object? value, Type objectType);

    AttributeValue SerializeDynamoDBValue<T>(T? value) => SerializeDynamoDBValue(value, typeof(T));

    DynamoDBAttributeInfo GetPropertyAttributeInfo((Type DeclaringType, string Name) property);

    DynamoDBAttributeInfo GetPropertyAttributeInfo(MemberInfo property) => GetPropertyAttributeInfo(property.AsSimplePropertyReference());

    DynamoDBObjectTypeNameResolver ObjectTypeNameResolver { get; }
}

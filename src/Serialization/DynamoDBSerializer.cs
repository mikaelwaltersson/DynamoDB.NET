using System.Collections;
using System.Reflection;
using Amazon.DynamoDBv2.Model;

namespace DynamoDB.Net.Serialization;

public class DynamoDBSerializer : IDynamoDBSerializer
{
    public object DeserializeDynamoDBValue(AttributeValue value, Type objectType)
    {
        throw new NotImplementedException();
    }

    public string GetSerializedPropertyName(MemberInfo property)
    {
        throw new NotImplementedException();
    }

    public AttributeValue SerializeDynamoDBValue(object? value, Type? objectType, SerializeDynamoDBValueTarget target = default)
    {
        throw new NotImplementedException();
    }

    public bool TryCreateDynamoDBSet(Type elementType, IEnumerable values, out object dynamoDBSet)
    {
        throw new NotImplementedException();
    }
}

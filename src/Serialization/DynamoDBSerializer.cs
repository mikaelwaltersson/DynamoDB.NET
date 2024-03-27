using System.Collections;
using System.Reflection;
using Amazon.DynamoDBv2.Model;

namespace DynamoDB.Net.Serialization;

public class DynamoDBSerializer : IDynamoDBSerializer
{
    public object? DeserializeDynamoDBValue(AttributeValue value, Type objectType) =>
        value switch
        {
            { NULL: true } => null,

            { IsBOOLSet: true } => Convert(value.BOOL, objectType),

            { S: not null } => Convert(value.S, objectType),

            { B: not null } => Convert(value.B.ToArray(), objectType),

            { N: not null } => ConvertNumber(value.N, objectType),

            _=> throw new NotImplementedException()
        };

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

    protected object? Convert<T>(T value, Type type)
    {
        // TODO: handle converters
        return CastConvert.CastTo(value, type);
    }

    protected object? ConvertNumber(string value, Type type)
    {
        // TODO: handle converters
        return NumberConvert.StringToNumber(value, type);
    }
}

using Amazon.DynamoDBv2.Model;

namespace DynamoDB.Net.Serialization.Converters;

public interface IConvertToDynamoDBValue
{
    public AttributeValue ConvertToDynamoDBValue(object? value, Type fromType, IDynamoDBSerializer serializer);
}

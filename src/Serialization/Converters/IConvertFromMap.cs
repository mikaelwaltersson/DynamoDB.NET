using Amazon.DynamoDBv2.Model;

namespace DynamoDB.Net.Serialization.Converters;

public interface IConvertFromMap
{
    object ConvertFromMap(Dictionary<string, AttributeValue> entries, Type toType, IDynamoDBSerializer serializer);
}

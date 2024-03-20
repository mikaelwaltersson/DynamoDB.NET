using Amazon.DynamoDBv2.Model;

namespace DynamoDB.Net.Serialization.Converters;

public interface IConvertFromList
{
    object ConvertFromList(List<AttributeValue> elements, Type toType, IDynamoDBSerializer serializer);
}

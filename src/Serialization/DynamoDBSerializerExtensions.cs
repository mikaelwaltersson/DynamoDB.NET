using Amazon.DynamoDBv2.Model;

namespace DynamoDB.Net.Serialization;

static class DynamoDBSerializerExtensions
{
    public static object? DeserializeDynamoDBValue(this IDynamoDBSerializer serializer, AttributeValue value, Type type, string pathElement)
    {
        try
        {
            return serializer.DeserializeDynamoDBValue(value, type);
        }
        catch (DynamoDBSerializationException ex)
        {
            ex.PrependPath(pathElement);
            throw;
        }
    }

    public static T? DeserializeDynamoDBValue<T>(this IDynamoDBSerializer serializer, AttributeValue value, string pathElement)
    {
        try
        {
            return serializer.DeserializeDynamoDBValue<T>(value);
        }
        catch (DynamoDBSerializationException ex)
        {
            ex.PrependPath(pathElement);
            throw;
        }
    }
}

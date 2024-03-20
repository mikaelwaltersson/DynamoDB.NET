namespace DynamoDB.Net.Serialization.Converters;

public abstract class DynamoDBTypeConverter
{
    public abstract bool Handle(Type type);
}

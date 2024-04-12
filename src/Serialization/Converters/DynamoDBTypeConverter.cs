namespace DynamoDB.Net.Serialization.Converters;

public abstract class DynamoDBTypeConverter
{
    public static DynamoDBTypeConverter Default { get; } = new DefaultDynamoDBTypeConverter();

    public abstract bool Handle(Type type);
}

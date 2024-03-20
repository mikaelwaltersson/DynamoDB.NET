namespace DynamoDB.Net.Serialization;

public sealed class DynamoDBAttributeInfo
{
    public required string AttributeName { get; init; }

    public bool IsPrimaryKey { get; set; }

    public bool SerializeDefaultValues { get; init; }

    public bool SerializeNullValues { get; init; }

    public bool NotSerialized { get; init; }

    public IEnumerable<IOnDeserializeProperty> OnDeserializeProperty { get; init; } = [];

    public IEnumerable<IOnSerializeProperty> OnSerializeProperty { get; init; } = [];
}

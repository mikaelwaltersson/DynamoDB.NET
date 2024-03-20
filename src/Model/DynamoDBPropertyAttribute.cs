namespace DynamoDB.Net.Model;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class DynamoDBPropertyAttribute : Attribute
{
    (bool Value, bool IsSpecified) serializeDefaultValues;
    (bool Value, bool IsSpecified) serializeNullValues;

    public string? AttributeName { get; init; }

    public bool SerializeDefaultValues 
    { 
        get => serializeDefaultValues.Value;
        set => serializeDefaultValues = (value, true);
    }

    public bool SerializeDefaultValuesIsSpecified => serializeDefaultValues.IsSpecified;

    public bool SerializeNullValues 
    { 
        get => serializeNullValues.Value;
        set => serializeNullValues = (value, true);
    }

    public bool SerializeNullValuesIsSpecified => serializeNullValues.IsSpecified;

    public bool NotSerialized { get; init; } 
}

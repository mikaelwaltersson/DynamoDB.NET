using System;

namespace DynamoDB.Net.Model;

[AttributeUsage(AttributeTargets.Property)]
public class DynamoDBPropertyAttribute : Attribute
{
    public string PropertyName { get; init; }

    public bool SerializeDefaultValues { get; init; }

    public bool SerializeNullValues { get; init; }
}

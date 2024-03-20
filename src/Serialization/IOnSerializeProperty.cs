namespace DynamoDB.Net.Serialization;

public interface IOnSerializeProperty
{
    object? OnSerialize<T>(T target, string propertyName, Func<T, object?> getValue) where T : notnull;
}

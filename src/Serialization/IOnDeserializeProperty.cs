namespace DynamoDB.Net.Serialization;

public interface IOnDeserializeProperty
{
    void OnDeserialize<T>(T target, string propertyName, object? value, Action<T, object?> setValue) where T : notnull;
}

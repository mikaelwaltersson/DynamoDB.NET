namespace DynamoDB.Net.Serialization;

static class DynamoDBAttributeInfoExtensions
{
    public static void SetPropertyValue<T>(this DynamoDBAttributeInfo attributeInfo, T target, string propertyName, object? value, Action<T, object?> setValue) where T : notnull =>
        attributeInfo.OnDeserializeProperty.Aggregate(setValue, (setValue, handler) => (target, value) => handler.OnDeserialize(target, propertyName, value, setValue))(target, value);

    public static object? GetPropertyValue<T>(this DynamoDBAttributeInfo attributeInfo, T target, string propertyName, Func<T, object?> getValue) where T : notnull =>
        attributeInfo.OnSerializeProperty.Aggregate(getValue, (getValue, handler) => target => handler.OnSerialize(target, propertyName, getValue))(target);
}

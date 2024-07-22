namespace DynamoDB.Net.Serialization.Converters;

static class TypeAssert
{
    public static object? ForNull(Type toType)
    {
        if (toType.IsValueType && Nullable.GetUnderlyingType(toType) == null)
            throw new DynamoDBSerializationException($"Type can not be deserialized from null: {toType}");

        return null;
    }

    public static object ForValue<T>(T value, Type toType) where T : notnull
    {
        if (!toType.IsAssignableFrom(typeof(T)))
            throw new DynamoDBSerializationException($"Type can not be deserialized from {typeof(T).Name}: {toType.FullName}");

        return value;
    }
}

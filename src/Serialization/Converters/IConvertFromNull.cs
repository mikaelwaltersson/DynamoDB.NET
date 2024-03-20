namespace DynamoDB.Net.Serialization.Converters;

public interface IConvertFromNull
{
    object? ConvertFromNull(Type toType);
}

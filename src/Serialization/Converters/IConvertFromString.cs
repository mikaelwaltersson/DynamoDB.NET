namespace DynamoDB.Net.Serialization.Converters;

public interface IConvertFromString
{
    object ConvertFromString(string value, Type toType);
}

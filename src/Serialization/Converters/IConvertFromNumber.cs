namespace DynamoDB.Net.Serialization.Converters;

public interface IConvertFromNumber
{
    object ConvertFromNumber(string value, Type toType);
}

namespace DynamoDB.Net.Serialization.Converters;

public interface IConvertFromBoolean
{
    object ConvertFromBoolean(bool value, Type toType);
}

namespace DynamoDB.Net.Serialization.Converters;

public interface IConvertFromBinary
{
    object ConvertFromBinary(MemoryStream value, Type toType);
}

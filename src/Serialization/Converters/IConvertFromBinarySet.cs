namespace DynamoDB.Net.Serialization.Converters;

public interface IConvertFromBinarySet
{
    object ConvertFromBinarySet(ICollection<MemoryStream> values, Type toType, IConvertFromBinary convertFromBinary);
}

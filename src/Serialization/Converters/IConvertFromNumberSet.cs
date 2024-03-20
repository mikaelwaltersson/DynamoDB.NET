namespace DynamoDB.Net.Serialization.Converters;

public interface IConvertFromNumberSet
{
    object ConvertFromNumberSet(ICollection<string> values, Type toType, IConvertFromNumber convertFromNumber);
}

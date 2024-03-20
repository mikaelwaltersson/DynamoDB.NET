namespace DynamoDB.Net.Serialization.Converters;

public interface IConvertFromStringSet
{
    object ConvertFromStringSet(ICollection<string> values, Type toType, IConvertFromString convertFromString);
}

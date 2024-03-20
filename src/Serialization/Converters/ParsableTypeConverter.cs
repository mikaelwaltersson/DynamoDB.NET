
using System.Globalization;
using Amazon.DynamoDBv2.Model;

namespace DynamoDB.Net.Serialization.Converters;

public abstract class ParsableTypeConverter : DynamoDBTypeConverter, IConvertFromString, IConvertToDynamoDBValue
{
    public abstract object ConvertFromString(string value, Type toType);
    
    public abstract AttributeValue ConvertToDynamoDBValue(object? value, Type fromType, IDynamoDBSerializer serializer);
}

public class ParsableTypeConverter<T> : ParsableTypeConverter
    where T : IParsable<T>, IFormattable
{
    public string Format { get; init; } = string.Empty;

    public override bool Handle(Type type) => type.UnwrapNullableType() == typeof(T);

    public override object ConvertFromString(string value, Type toType) =>
        T.Parse(value, CultureInfo.InvariantCulture);

    public override AttributeValue ConvertToDynamoDBValue(object? value, Type fromType, IDynamoDBSerializer serializer) =>
        DefaultDynamoDBTypeConverter.Instance.ConvertToDynamoDBValue(
            value != null ? ((T)value).ToString(Format, CultureInfo.InvariantCulture) : null,
            typeof(string),
            serializer);
}

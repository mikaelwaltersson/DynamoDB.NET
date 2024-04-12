using System.Collections.Concurrent;
using System.Globalization;
using Amazon.DynamoDBv2.Model;

namespace DynamoDB.Net.Serialization.Converters;

public class EnumTypeConverter
    : DynamoDBTypeConverter, IConvertFromNumber, IConvertFromString, IConvertToDynamoDBValue
{
    EnumParser parser = new();

    public NameTransform EnumNameTransform 
    { 
        get => parser.NameTransform;
        set => parser = new EnumParserWithNameTransform(value); 
    }

    public override bool Handle(Type type) => type.UnwrapNullableType().IsEnum;

    public object ConvertFromNumber(string value, Type toType) =>
        Enum.ToObject(toType, long.Parse(value, CultureInfo.InvariantCulture));

    public object ConvertFromString(string value, Type toType) =>
        parser.Parse(value, toType.UnwrapNullableType());

    public AttributeValue ConvertToDynamoDBValue(object? value, Type fromType, IDynamoDBSerializer serializer)
    {
        if (value == null)
            return new() { NULL = true };

        var formattedValue = parser.Format(value, value.GetType());

        return formattedValue.All(char.IsDigit)
            ? new() { N = formattedValue }
            : new() { S = formattedValue };
    }

    class EnumParser
    {
        public virtual NameTransform NameTransform => NameTransform.Default;

        public virtual object Parse(string value, Type toType) => 
            Enum.Parse(toType.UnwrapNullableType(), value);

        public virtual string Format(object value, Type fromType) => 
            Enum.Format(fromType, value, "g");
    }

    class EnumParserWithNameTransform(NameTransform nameTransform) : EnumParser
    {
        readonly ConcurrentDictionary<Type, (Dictionary<string, string> Parse, Dictionary<string, string> Format)> transforms = new();

        public override NameTransform NameTransform => nameTransform;

        public override object Parse(string value, Type toType) =>
            base.Parse(ApplyTransform(value, transforms.GetOrAdd(toType, CreateTransformLookup).Parse), toType);

        public override string Format(object value, Type fromType) =>
            ApplyTransform(base.Format(value, fromType), transforms.GetOrAdd(fromType, CreateTransformLookup).Format);

        (Dictionary<string, string> Parse, Dictionary<string, string> Format) CreateTransformLookup(Type type) => (
            Parse: Enum.GetNames(type).Select(value => (nameTransform.TransformName(value), value)).ToDictionary(),
            Format: Enum.GetNames(type).Select(value => (value, nameTransform.TransformName(value))).ToDictionary());

        static string ApplyTransform(string value, Dictionary<string, string> transform)
        {
            if (transform.TryGetValue(value, out var transformedValue))
                return transformedValue;

            if (value.Contains(','))
                return string.Join(", ", value.Split(',').Select(flag => flag.Trim()).Select(flag => ApplyTransform(flag, transform)));
            
            return value;
        }
    }
}

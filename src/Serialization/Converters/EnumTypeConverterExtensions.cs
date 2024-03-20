namespace DynamoDB.Net.Serialization.Converters;

static class EnumTypeConverterExtensions
{
    public static void SetNameTransform(this IEnumerable<EnumTypeConverter> typeConverters, NameTransform nameTransform)
    {
        foreach (var converter in typeConverters)
        {
            converter.EnumNameTransform = nameTransform;
        }
    }
}

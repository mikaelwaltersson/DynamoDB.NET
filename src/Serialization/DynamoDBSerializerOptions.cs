using DynamoDB.Net.Serialization.Converters;

namespace DynamoDB.Net.Serialization;

public record DynamoDBSerializerOptions
{
    public NameTransform AttributeNameTransform { get; set; } = NameTransform.Default;

    public NameTransform EnumValueNameTransform { set => TypeConverters.OfType<EnumTypeConverter>().SetNameTransform(value); }

    public List<DynamoDBTypeConverter> TypeConverters { get; set; } = 
        [
            new ParsableTypeConverter<DateOnly> { Format = "o" },
            new ParsableTypeConverter<DateTime> { Format = "o" },
            new ParsableTypeConverter<DateTimeOffset> { Format = "o" },
            new ParsableTypeConverter<Guid> { Format = "d" },
            new ParsableTypeConverter<TimeOnly> { Format = "o" },
            new ParsableTypeConverter<TimeSpan> { Format = "c" },
            new EnumTypeConverter(),
            new JsonNodeTypeConverter()
        ];

    public bool SerializeDefaultValues { get; set; }

    public SerializeDefaultValuesForDelegate? SerializeDefaultValuesFor { get; set; }

    public bool SerializeNullValues { get; set; }

    public DynamoDBObjectTypeNameResolver ObjectTypeNameResolver { get; set; } = DynamoDBObjectTypeNameResolver.Default;

    public List<IOnDeserializeProperty> OnDeserializeProperty { get; set; } = [];

    public List<IOnSerializeProperty> OnSerializeProperty { get; set; } = [];
}

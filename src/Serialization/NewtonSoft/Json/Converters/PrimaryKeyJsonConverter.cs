using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace DynamoDB.Net.Serialization.Newtonsoft.Json.Converters;

public class PrimaryKeyJsonConverter<T> : JsonConverter where T : class
{
    JsonProperty[] properties;

    public PrimaryKeyJsonConverter(JsonContractResolver contractResolver)
    {
        ArgumentNullException.ThrowIfNull(contractResolver);

        var tableDescription = contractResolver.GetTableDescription(typeof(T));

        this.properties =
            new[]
            {
                JsonContractResolver.UnwrapJsonProperty(tableDescription.PartitionKeyProperty).Clone(required: Required.Always),
                JsonContractResolver.UnwrapJsonProperty(tableDescription.SortKeyProperty)?.Clone(required: Required.Always)
            };
    }


    public override bool CanConvert(Type objectType) => (objectType == typeof(PrimaryKey<T>));

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        var tokenType = reader.TokenType;

        if (tokenType == JsonToken.Null)
            return null;

        reader.ExpectTokenType(JsonToken.StartObject);

        var values = reader.ReadProperties(properties, serializer);

        return new PrimaryKey<T>(values[0], values[1]);
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        var primaryKey = (PrimaryKey<T>)value;

        if (primaryKey == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteStartObject();
        properties[0].WriteProperty(writer, primaryKey.PartitionKey, serializer);
        properties[1]?.WriteProperty(writer, primaryKey.SortKey, serializer);
        writer.WriteEndObject();
    }
}

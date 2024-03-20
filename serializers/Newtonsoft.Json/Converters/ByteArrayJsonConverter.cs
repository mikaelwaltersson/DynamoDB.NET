using System;
using Newtonsoft.Json;

namespace DynamoDB.Net.Serialization.Newtonsoft.Json.Converters;

public class ByteArrayJsonConverter : JsonConverter
{
    public override bool CanConvert(Type objectType) => objectType == typeof(byte[]);

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        if (reader.Value is byte[])
            return reader.Value;

        return ((string)reader.Value)?.ToByteArrayFromBase64();
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        writer.WriteValue((byte[])value);
    }
}

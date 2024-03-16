using System;
using System.IO;

using Newtonsoft.Json;

namespace DynamoDB.Net.Serialization.Newtonsoft.Json.Converters
{
    public class MemoryStreamJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(MemoryStream);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.Value is byte[])
                return new MemoryStream((byte[])reader.Value);

            return ((string)reader.Value)?.ToMemoryStreamFromBase64();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteValue(((MemoryStream)value)?.ToArray());
        }
    }
}
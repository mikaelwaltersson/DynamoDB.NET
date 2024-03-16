using System;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace DynamoDB.Net.Serialization.Newtonsoft.Json
{
    public static class JsonReaderExtensions
    {
        public static void ExpectTokenType(this JsonReader reader, JsonToken expectedTokenType)
        {
            if (reader.TokenType != expectedTokenType)
                throw new JsonReaderException($"Expected {expectedTokenType}, got: {reader.TokenType}");
        }

        public static object ReadProperty(this JsonReader reader, JsonProperty property, JsonSerializer serializer)
        {
            if (property == null)
                throw new ArgumentNullException(nameof(property));

            if (serializer == null)
                throw new ArgumentNullException(nameof(serializer));

            var converter = property.Converter ?? serializer.ContractResolver.ResolveContract(property.PropertyType).Converter;
            if (converter != null)
                return converter.ReadJson(reader, property.PropertyType, property.DefaultValue, serializer);

            if (reader.TokenType == JsonToken.Null || property.IsPrimitivePropertyType())
                return reader.Value.CastTo(property.PropertyType);

            return serializer.Deserialize(reader, property.PropertyType);
        }

        public static object[] ReadProperties(this JsonReader reader, JsonProperty[] properties, JsonSerializer serializer)
        {
            if (properties == null)
                throw new ArgumentNullException(nameof(properties));

            if (serializer == null)
                throw new ArgumentNullException(nameof(serializer));

            var values = new object[properties.Length];
            var hasBeenRead = new bool[properties.Length];


            while (reader.Read() && reader.TokenType != JsonToken.EndObject)
            {
                reader.ExpectTokenType(JsonToken.PropertyName);
                
                var propertyName = (string)reader.Value;

                if (!reader.Read())
                    throw new JsonReaderException($"Unexpected EOF");

                var i = Array.FindIndex(properties, p => p != null && p.PropertyName == propertyName);
                if (i < 0)
                {
                    reader.Skip();
                    continue;
                }

                var property = properties[i];

                hasBeenRead[i] = true;
                values[i] = reader.ReadProperty(property, serializer);
            }

            for (var i = 0; i < properties.Length; i++)
            {
                var required = properties[i]?.Required;

                if (!hasBeenRead[i] && (required == Required.AllowNull || required == Required.Always))
                    throw new JsonReaderException($"Missing required property '{properties[i].PropertyName}'");

                if (properties[i] == null && (required == Required.DisallowNull || required == Required.Always))
                    throw new JsonReaderException($"Null is not valid value for property '{properties[i].PropertyName}'");
            }

            return values;
        }
    }
}
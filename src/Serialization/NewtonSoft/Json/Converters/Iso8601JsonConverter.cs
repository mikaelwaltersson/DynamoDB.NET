using System;

using Newtonsoft.Json;

namespace DynamoDB.Net.Serialization.Newtonsoft.Json.Converters
{
    public class Iso8601JsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            objectType = Nullable.GetUnderlyingType(objectType) ?? objectType;

            return 
                objectType == typeof(DateTime) ||
                objectType == typeof(DateTimeOffset) ||
                objectType == typeof(TimeSpan);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {            
            objectType = Nullable.GetUnderlyingType(objectType) ?? objectType;

            var value = reader.Value;
            
            if (value is string)
            {
                if (objectType == typeof(DateTime))
                    return ((string)value).ToIso8601DateTime();

                if (objectType == typeof(DateTimeOffset))
                    return ((string)value).ToIso8601DateTimeOffset();

                if (objectType == typeof(TimeSpan))
                    return ((string)value).ToIso8601TimeSpan();
            }
            else if (value is DateTimeOffset && objectType == typeof(DateTime))
            {
                return ((DateTimeOffset)value).LocalDateTime;
            }
            else if (value is DateTime && objectType == typeof(DateTimeOffset))
            {
                return (DateTimeOffset)(DateTime)value;
            }

            return value;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var objectType = value?.GetType();
            
            if (objectType == typeof(DateTime))
                value = ((DateTime)value).ToIso8601String();

            if (objectType == typeof(DateTimeOffset))
                value = ((DateTimeOffset)value).ToIso8601String();

            if (objectType == typeof(TimeSpan))
                value = ((TimeSpan)value).ToIso8601String();

            writer.WriteValue(value);
        }
    }
}
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.DynamoDBv2.Model;

namespace DynamoDB.Net;

public class AttributeValueJsonConverter : JsonConverter<AttributeValue>
{
    public override AttributeValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        Assert(reader.TokenType is JsonTokenType.StartObject);
        var value = new AttributeValue();

        Assert(reader.Read() && reader.TokenType is JsonTokenType.PropertyName);
        var type = reader.GetString();
        
        switch (type)
        {
            case "NULL":
                Assert(reader.Read() && reader.TokenType is JsonTokenType.True);
                value.NULL = true;
                break;

            case "BOOL":
                Assert(reader.Read() && reader.TokenType is JsonTokenType.False or JsonTokenType.True);
                value.BOOL = reader.GetBoolean();
                break;

            case "S":
                Assert(reader.Read() && reader.TokenType is JsonTokenType.String);
                value.S = reader.GetString()!;
                Assert(value.S is not null);
                break;
        
            case "N":
                Assert(reader.Read() && reader.TokenType is JsonTokenType.String);
                value.N = reader.GetString()!;
                Assert(value.N is not null);
                break;

            case "B":
                Assert(reader.Read() && reader.TokenType is JsonTokenType.String);
                value.B = new MemoryStream(reader.GetBytesFromBase64());
                Assert(value.B is not null);
                break;

            case "SS":
                value.SS = [];
                Assert(reader.Read() && reader.TokenType is JsonTokenType.StartArray);
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    Assert(reader.TokenType is JsonTokenType.String);
                    value.SS.Add(reader.GetString());
                }
                Assert(reader.TokenType is JsonTokenType.EndArray);
                Assert(value.SS.Count > 0 && value.SS.All(s => s is not null));
                break;

            case "NS":
                value.NS = [];
                Assert(reader.Read() && reader.TokenType is JsonTokenType.StartArray);
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    Assert(reader.TokenType is JsonTokenType.String);
                    value.NS.Add(reader.GetString());
                }
                Assert(reader.TokenType is JsonTokenType.EndArray);
                Assert(value.NS.Count > 0 && value.NS.All(n => n is not null));
                break;

            case "BS":
                value.BS = [];
                Assert(reader.Read() && reader.TokenType is JsonTokenType.StartArray);
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    Assert(reader.TokenType is JsonTokenType.String);
                    value.BS.Add(new MemoryStream(reader.GetBytesFromBase64()));
                }
                Assert(reader.TokenType is JsonTokenType.EndArray);
                Assert(value.BS.Count > 0 && value.BS.All(b => b is not null));
                break;

            case "L":
                value.L = [];
                Assert(reader.Read() && reader.TokenType is JsonTokenType.StartArray);
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    Assert(reader.TokenType is JsonTokenType.StartObject);
                    value.L.Add(Read(ref reader, typeToConvert, options));
                }
                Assert(reader.TokenType is JsonTokenType.EndArray);
                break;

            case "M":
                value.M = [];
                Assert(reader.Read() && reader.TokenType is JsonTokenType.StartObject);
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    Assert(reader.TokenType is JsonTokenType.PropertyName);
                    var property = reader.GetString()!;

                    Assert(reader.Read() && reader.TokenType is JsonTokenType.StartObject);
                    value.M[property] = Read(ref reader, typeToConvert, options);
                }
                Assert(reader.TokenType is JsonTokenType.EndObject);
                break;

            default:
                throw new JsonException();
        }

        Assert(reader.Read() && reader.TokenType is JsonTokenType.EndObject);
        return value;
    }

    public override void Write(Utf8JsonWriter writer, AttributeValue value, JsonSerializerOptions options)
    {
        if (value.NULL)
        {
            writer.WriteStartObject();
            writer.WriteBoolean("NULL", true);
            writer.WriteEndObject();
        }
        else if (value.IsBOOLSet)
        {
            writer.WriteStartObject();
            writer.WriteBoolean("BOOL", value.BOOL);
            writer.WriteEndObject();
        }
        else if (value.S is not null)
        {
            writer.WriteStartObject();
            writer.WriteString("S", value.S);
            writer.WriteEndObject();
        }
        else if (value.N is not null)
        {
            writer.WriteStartObject();
            writer.WriteString("N", value.N);
            writer.WriteEndObject();
        }
        else if (value.B is not null)
        {
            writer.WriteStartObject();
            writer.WriteBase64String("B", value.B.ToArray());
            writer.WriteEndObject();
        }
        else if (value.SS.Count > 0)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("SS");
            writer.WriteStartArray();
            foreach (var entry in value.SS)
                writer.WriteStringValue(entry);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        else if (value.NS.Count > 0)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("NS");
            writer.WriteStartArray();
            foreach (var entry in value.NS)
                writer.WriteStringValue(entry);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        else if (value.BS.Count > 0)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("BS");
            writer.WriteStartArray();
            foreach (var entry in value.BS)
                writer.WriteBase64StringValue(entry.ToArray());
            writer.WriteEndArray();
            writer.WriteEndObject();
        }      
        else if (value.IsLSet)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("L");
            writer.WriteStartArray();
            foreach (var entry in value.L)
                JsonSerializer.Serialize(writer, entry, options);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        else if (value.IsMSet)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("M");
            writer.WriteStartObject();
            foreach (var entry in value.M)
            {
                writer.WritePropertyName(entry.Key);
                JsonSerializer.Serialize(writer, entry.Value, options);
            }
            writer.WriteEndObject();
            writer.WriteEndObject();
        }
        else 
            throw new JsonException();
    }

    static void Assert(bool condition)
    {
        if (!condition)
            throw new JsonException();
    }
}

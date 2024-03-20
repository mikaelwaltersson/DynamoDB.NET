using System.Text.Json;
using System.Text.Json.Nodes;
using Amazon.DynamoDBv2.Model;

namespace DynamoDB.Net.Serialization.Converters;

public class JsonNodeTypeConverter
    : DynamoDBTypeConverter,
    IConvertFromNull, IConvertFromBoolean,
    IConvertFromString, IConvertFromNumber, IConvertFromBinary,
    IConvertFromStringSet, IConvertFromNumberSet, IConvertFromBinarySet,
    IConvertFromList,
    IConvertFromMap,
    IConvertToDynamoDBValue
{
    public override bool Handle(Type type) => type == typeof(JsonNode);

    public object? ConvertFromNull(Type toType) =>
        null;

    public object ConvertFromBoolean(bool value, Type toType) =>
        JsonValue.Create(value);

    public object ConvertFromString(string value, Type toType) =>
        JsonValue.Create(value);

    public object ConvertFromNumber(string value, Type toType) =>
        JsonNode.Parse(value)!;

    public object ConvertFromBinary(MemoryStream value, Type toType) =>
        JsonValue.Create(value.ToBase64String());

    public object ConvertFromStringSet(ICollection<string> values, Type toType, IConvertFromString convertFromString) =>
        new JsonArray(values.Select(value => JsonValue.Create(value)).ToArray());

    public object ConvertFromNumberSet(ICollection<string> values, Type toType, IConvertFromNumber convertFromNumber) =>
        new JsonArray(values.Select(value => JsonNode.Parse(value)).ToArray());

    public object ConvertFromBinarySet(ICollection<MemoryStream> values, Type toType, IConvertFromBinary convertFromBinary) =>
        new JsonArray(values.Select(value => JsonValue.Create(value.ToBase64String())).ToArray());

    public object ConvertFromList(List<AttributeValue> elements, Type toType, IDynamoDBSerializer serializer) =>
        new JsonArray(elements.Select(serializer.DeserializeDynamoDBValue<JsonNode>).ToArray());

    public object ConvertFromMap(Dictionary<string, AttributeValue> entries, Type toType, IDynamoDBSerializer serializer) =>
       new JsonObject(entries.Select(entry => new KeyValuePair<string, JsonNode?>(entry.Key, serializer.DeserializeDynamoDBValue<JsonNode>(entry.Value))));

    public AttributeValue ConvertToDynamoDBValue(object? value, Type fromType, IDynamoDBSerializer serializer) => 
        value switch
        {
            null => new() { NULL = true },

            JsonValue jsonValue => jsonValue.GetValueKind() switch
            {
                JsonValueKind.False => new() { BOOL = false },

                JsonValueKind.True => new() { BOOL = true },
                
                JsonValueKind.String => new() { S = jsonValue.GetValue<string>() },
            
                JsonValueKind.Number => new() { N = jsonValue.ToJsonString() },
                
                _ => new()
            },

            JsonArray jsonArray => 
                new() 
                {
                    L = new(jsonArray.Select(serializer.SerializeDynamoDBValue)),
                    IsLSet = true
                },

            JsonObject jsonObject => 
                new() 
                { 
                    M = new(jsonObject.Select(entry => KeyValuePair.Create(entry.Key, serializer.SerializeDynamoDBValue(entry.Value)))),
                    IsMSet = true
                },

            _ => new()
        };
}

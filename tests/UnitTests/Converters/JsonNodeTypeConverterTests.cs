using System.Text;
using System.Text.Json.Nodes;
using DynamoDB.Net.Serialization;
using DynamoDB.Net.Serialization.Converters;
using Microsoft.Extensions.Options;

namespace DynamoDB.Net.Tests.UnitTests.Converters;

public class JsonNodeTypeConverterTests
{
    readonly DynamoDBSerializer serializer = 
        new(Options.Create(new DynamoDBSerializerOptions { TypeConverters = [new JsonNodeTypeConverter()] }));

    [Fact]
    public void CanSerializeDynamoDBValue()
    {
        Snapshot.Match(
            serializer.SerializeDynamoDBValue(
                JsonNode.Parse("""{ "foo": [null, true, false, 1, "2", ["3", "4"]], "bar": "SGVsbG9Xb3JsZAo=" }"""),
                typeof(JsonNode)));
    }

    [Fact]
    public void CanDeserializeDynamoDBValue()
    {
        Snapshot.Match(
            serializer.DeserializeDynamoDBValue(
                new()
                {
                    M = new()
                    {
                        ["foo"] = new()
                        {
                            L = new()
                            {
                                new() { NULL = true },
                                new() { BOOL = false },
                                new() { BOOL = true },
                                new() { N = "1" },
                                new() { S = "2" },
                                new() { SS = ["3", "4"] },
                            }
                        },
                        ["bar"] = new() { B = new(Encoding.ASCII.GetBytes("HelloWorld")) }
                    }
                },
                typeof(JsonNode)));
    }
}

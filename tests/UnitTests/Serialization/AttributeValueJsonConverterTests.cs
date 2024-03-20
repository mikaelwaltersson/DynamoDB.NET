using System.Text;
using System.Text.Json;
using Amazon.DynamoDBv2.Model;

namespace DynamoDB.Net.Tests.UnitTests.Serialization;

public class AttributeValueJsonConverterTests
{
    [Fact]
    public void CanDeserializeNULL()
    {
        Assert.True(Deserialize("""{ "NULL": true  }""") is { NULL: true });
    }

    [Fact]
    public void CanDeserializeBOOL()
    {
        Assert.True(Deserialize("""{ "BOOL": true  }""") is { IsBOOLSet: true, BOOL: true });
        Assert.True(Deserialize("""{ "BOOL": false  }""") is { IsBOOLSet: true, BOOL: false });
    }

    [Fact]
    public void CanDeserializeS()
    {
        Assert.True(Deserialize("""{ "S": "Hello World"  }""") is { S: "Hello World" });
    }

    [Fact]
    public void CanDeserializeN()
    {
        Assert.True(Deserialize("""{ "N": "123.456"  }""") is { N: "123.456" });
    }

    [Fact]
    public void CanDeserializeB()
    {
        Assert.True(
            Deserialize("""{ "B": "SGVsbG8gV29ybGQ="  }""") is { B: MemoryStream stream } &&
            Encoding.UTF8.GetString(stream.ToArray()) is "Hello World");
    }

    [Fact]
    public void CanDeserializeSS()
    {
        Assert.True(Deserialize("""{ "SS": ["Hello", "World"]  }""") is { SS: ["Hello", "World"] });
    }

    [Fact]
    public void CanDeserializeNS()
    {
        Assert.True(Deserialize("""{ "NS": ["12.34", "5.6"]  }""") is { NS: ["12.34", "5.6"] });
    }

    [Fact]
    public void CanDeserializeBS()
    {
        Assert.True(
            Deserialize("""{ "BS": ["SGVsbG8gV29ybGQ="]  }""") is { BS: [MemoryStream stream] } &&
            Encoding.UTF8.GetString(stream.ToArray()) is "Hello World");
    }

    [Fact]
    public void CanDeserializeL()
    {
        Assert.True(Deserialize("""{ "L": []  }""") is { L: [] });
        Assert.True(Deserialize("""{ "L": [{ "S": "Hello" }, { "S": "World" }]  }""") is { L: [{ S: "Hello" }, { S: "World" }] });
    }

    [Fact]
    public void CanDeserializeM()
    {
        Assert.True(Deserialize("""{ "M": {}  }""") is { M.Count: 0 });
        Assert.True(
            Deserialize("""{ "M": { "text": { "S": "Hello World"  } } }""") is { M: { Count: 1 } values } &&
            values["text"] is { S: "Hello World" });
    }

    [Fact]
    public void CanSerializeNULL()
    {
        Assert.True(Serialize(new() { NULL = true }) is """{"NULL":true}""");
    }

    [Fact]
    public void CanSerializeBOOL()
    {
        Assert.Equal("""{"BOOL":false}""", Serialize(new() { BOOL = false }));
        Assert.Equal("""{"BOOL":true}""", Serialize(new() { BOOL = true }));
    }

    [Fact]
    public void CanSerializeS()
    {
        Assert.Equal("""{"S":"Hello World"}""", Serialize(new() { S = "Hello World" }));
    }

    [Fact]
    public void CanSerializeN()
    {
        Assert.Equal("""{"N":"123.456"}""", Serialize(new() { N = "123.456" }));
    }

    [Fact]
    public void CanSerializeB()
    {
        Assert.Equal("""{"B":"SGVsbG8gV29ybGQ="}""", Serialize(new() { B = new MemoryStream(Encoding.UTF8.GetBytes("Hello World")) }));
    }

    [Fact]
    public void CanSerializeSS()
    {
        Assert.Equal("""{"SS":["Hello","World"]}""", Serialize(new() { SS = ["Hello", "World"] }));
    }

    [Fact]
    public void CanSerializeNS()
    {
        Assert.Equal("""{"NS":["12.34","5.6"]}""", Serialize(new() { NS = ["12.34", "5.6"] }));
    }

    [Fact]
    public void CanSerializeBS()
    {
        Assert.Equal("""{"BS":["SGVsbG8gV29ybGQ="]}""", Serialize(new() { BS = [new MemoryStream(Encoding.UTF8.GetBytes("Hello World"))] }));
    }

    [Fact]
    public void CanSerializeL()
    {
        Assert.Equal("""{"L":[]}""", Serialize(new() { IsLSet = true }));
        Assert.Equal("""{"L":[{"S":"Hello"},{"S":"World"}]}""", Serialize(new() { L = [new() { S = "Hello" }, new() { S = "World" }] }));
    }

    [Fact]
    public void CanSerializeM()
    {
        Assert.Equal("""{"M":{}}""", Serialize(new() { IsMSet = true }));
        Assert.Equal("""{"M":{"text":{"S":"Hello World"}}}""", Serialize(new() { M = { ["text"] = new() { S = "Hello World" } } }));
    }


    static readonly JsonSerializerOptions serializerOptions = new() { Converters = { new AttributeValueJsonConverter() } };

    static AttributeValue? Deserialize(string json) => JsonSerializer.Deserialize<AttributeValue>(json, serializerOptions);

    static string Serialize(AttributeValue value) => JsonSerializer.Serialize(value, serializerOptions);
}

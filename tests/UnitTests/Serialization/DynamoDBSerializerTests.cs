using System.Numerics;
using System.Runtime.InteropServices;
using DynamoDB.Net.Serialization;

namespace DynamoDB.Net.Tests.UnitTests.Serialization;

public class DynamoDBSerializerTests
{
    readonly IDynamoDBSerializer serializer = new DynamoDBSerializer(); 

    [Fact]
    public void CanDeserializeNULL()
    {
        Assert.Null(serializer.DeserializeDynamoDBValue(new() { NULL = true }, typeof(object)));
    }

    [Fact]
    public void CanDeserializeBOOL()
    {
        Assert.False(serializer.DeserializeDynamoDBValue<bool?>(new() { BOOL = false }));
        Assert.True(serializer.DeserializeDynamoDBValue<bool?>(new() { BOOL = true }));
    }

    [Fact]
    public void CanDeserializeS()
    {
        Assert.Equal("Hello World", serializer.DeserializeDynamoDBValue<string?>(new() { S = "Hello World" }));
    }

    [Theory, MemberData(nameof(GetNumberData))]
    public void CanDeserializeN(Type numberType, string numberText, object numberValue)
    {
        Assert.Equal(numberValue, serializer.DeserializeDynamoDBValue(new() { N = numberText }, numberType));
    }

    [Fact]
    public void CanDeserializeB()
    {
        Assert.True(serializer.DeserializeDynamoDBValue<byte[]>(new() { B = new MemoryStream([1, 2, 3, 4, 5]) }) is [1, 2, 3, 4, 5]);
    }

    [Fact]
    public void CanDeserializeSS()
    {

    }

    [Fact]
    public void CanDeserializeNS()
    {

    }

    [Fact]
    public void CanDeserializeBS()
    {

    }

    [Fact]
    public void CanDeserializeL()
    {

    }

    [Fact]
    public void CanDeserializeM()
    {

    }

    [Fact]
    public void CanSerializeNULL()
    {

    }

    [Fact]
    public void CanSerializeBOOL()
    {

    }

    [Fact]
    public void CanSerializeS()
    {

    }

    [Fact]
    public void CanSerializeN()
    {

    }

    [Fact]
    public void CanSerializeB()
    {
    
    }

    [Fact]
    public void CanSerializeSS()
    {

    }

    [Fact]
    public void CanSerializeNS()
    {

    }

    [Fact]
    public void CanSerializeBS()
    {

    }

    [Fact]
    public void CanSerializeL()
    {

    }

    [Fact]
    public void CanSerializeM()
    {

    }

    static IEnumerable<object[]> GetNumberData() =>
        [
            [typeof(decimal), "123.456", 123.456M],
            [typeof(byte), "123", (byte)123],
            [typeof(double), "123.456", 123.456],
            [typeof(Half), "123.456", Half.CreateChecked(123.456f)],
            [typeof(short), "12345", (short)12345],
            [typeof(int), "123456", 123456],
            [typeof(long), "123456", 123456L],
            [typeof(Int128), "123456",  Int128.CreateChecked(123456)],
            [typeof(nint), "123456", nint.CreateChecked(123456)],
            [typeof(sbyte), "123", (sbyte)123],
            [typeof(float), "123.456", 123.456F],
            [typeof(ushort), "12345", (ushort)12345uL],
            [typeof(uint), "123456", 123456U],
            [typeof(ulong), "123456", 123456UL],
            [typeof(UInt128), "123456", UInt128.CreateChecked(123456)],
            [typeof(nuint), "123456", nuint.CreateChecked(123456)],
            [typeof(NFloat), "123.456", NFloat.CreateChecked(123.456)],
            [typeof(BigInteger), "123456", BigInteger.CreateChecked(123456)]
        ];
}

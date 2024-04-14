using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.InteropServices;
using DynamoDB.Net.Model;
using DynamoDB.Net.Serialization;
using Microsoft.Extensions.Options;

namespace DynamoDB.Net.Tests.UnitTests.Serialization;

public class DynamoDBSerializerTests
{
    readonly DynamoDBSerializerOptions options = new();
    
    IDynamoDBSerializer Serializer => new DynamoDBSerializer(Options.Create(options)); 

    [Fact]
    public void CanDeserializeNULL()
    {
        Assert.Null(Serializer.DeserializeDynamoDBValue(new() { NULL = true }, typeof(object)));
    }

    [Fact]
    public void CanDeserializeBOOL()
    {
        Assert.False(Serializer.DeserializeDynamoDBValue<bool>(new() { BOOL = false }));
        Assert.True(Serializer.DeserializeDynamoDBValue<bool>(new() { BOOL = true }));
    }

    [Fact]
    public void CanDeserializeToNullableBooleanFromBOOL()
    {
        Assert.True(Serializer.DeserializeDynamoDBValue(new() { BOOL = false }, typeof(bool?)) is false);
        Assert.True(Serializer.DeserializeDynamoDBValue(new() { BOOL = true }, typeof(bool?)) is true);
        Assert.True(Serializer.DeserializeDynamoDBValue(new() { NULL = true }, typeof(bool?)) is null);
    }

    [Fact]
    public void CanDeserializeS()
    {
        Assert.Equal(
            "Hello World", 
            Serializer.DeserializeDynamoDBValue<string?>(new() { S = "Hello World" }));
    }

    [Fact]
    public void CanDeserializeAsParsableTypeFromS()
    {
        Assert.Equal(
            new DateOnly(2024, 3, 31), 
            Serializer.DeserializeDynamoDBValue<DateOnly>(new() { S = "2024-03-31" }));
    }

    [Fact]
    public void CanDeserializeAsCustomParsableTypeFromS()
    {
        Assert.Equal(
            new CustomParsable('F', 'O', 'O'), 
            Serializer.DeserializeDynamoDBValue<CustomParsable>(new() { S = "FOO" }));
    }

    [Fact]
    public void CanDeserializeAsEnumTypeFromS()
    {
        Assert.Equal(
            FileAccess.ReadWrite, 
            Serializer.DeserializeDynamoDBValue<FileAccess>(new() { S = "ReadWrite" }));
    }

    [Fact]
    public void CanDeserializeAsEnumTypeWithNameTransformFromS()
    {
        options.EnumValueNameTransform = NameTransform.SnakeCase;

        Assert.Equal(
            AttributeTargets.Parameter | AttributeTargets.ReturnValue | AttributeTargets.GenericParameter, 
            Serializer.DeserializeDynamoDBValue<AttributeTargets>(new() { S = "parameter, return_value, generic_parameter" }));
    }

    [Fact]
    public void CanDeserializeAsNullableEnumTypeFromS()
    {
        Assert.Equal(
            FileAccess.ReadWrite, 
            Serializer.DeserializeDynamoDBValue<FileAccess?>(new() { S = "ReadWrite" }));

        Assert.Null(
            Serializer.DeserializeDynamoDBValue<FileAccess?>(new() { NULL = true }));
    }

    [Theory, MemberData(nameof(GetNumberData))]
    public void CanDeserializeN(Type numberType, string numberText, object numberValue)
    {
        Assert.Equal(
            numberValue, 
            Serializer.DeserializeDynamoDBValue(new() { N = numberText }, numberType));
    }

    [Theory, MemberData(nameof(GetNumberData))]
    public void CanDeserializeAsNullableNumberFromN(Type numberType, string numberText, object numberValue)
    {
        Assert.Equal(
            numberValue, 
            Serializer.DeserializeDynamoDBValue(new() { N = numberText }, typeof(Nullable<>).MakeGenericType(numberType)));

        Assert.Null(
            Serializer.DeserializeDynamoDBValue(new() { NULL = true }, typeof(Nullable<>).MakeGenericType(numberType)));
    }

    [Fact]
    public void CanDeserializeEnumTypeFromN()
    {
        Assert.Equal(
            (AttributeTargets)32768, 
            Serializer.DeserializeDynamoDBValue<AttributeTargets>(new() { N = "32768" }));
    }

    [Fact]
    public void CanDeserializeAsEnumTypeWithNameTransformFromN()
    {
        options.EnumValueNameTransform = NameTransform.SnakeCase;

        Assert.Equal(
            (AttributeTargets)32768, 
            Serializer.DeserializeDynamoDBValue<AttributeTargets>(new() { N = "32768" }));
    }

    [Fact]
    public void CanDeserializeB()
    {
        Assert.Equal(
            [1, 2, 3, 4, 5, 6], 
            Serializer.DeserializeDynamoDBValue<byte[]>(new() { B = new MemoryStream([1, 2, 3, 4, 5, 6]) }));
    }

    [Fact]
    public void CanDeserializeAsParsableTypeFromSS()
    {
        Assert.Equal(
            [new DateOnly(2024, 3, 30), new DateOnly(2024, 3, 31)], 
            Serializer.DeserializeDynamoDBValue<ISet<DateOnly>>(new() { SS = ["2024-03-30", "2024-03-31"] }));
    }

    [Fact]
    public void CanDeserializeAsEnumTypeFromSS()
    {
        Assert.Equal(
            [FileAccess.Read, FileAccess.Write], 
            Serializer.DeserializeDynamoDBValue<ISet<FileAccess>>(new() { SS = ["Read", "Write"] }));
    }

    [Fact]
    public void CanDeserializeSS()
    {
        Assert.Equal(
            ["Hello", "World"], 
            Serializer.DeserializeDynamoDBValue<ISet<string>>(new() { SS = ["Hello", "World"] }));
    }

    [Theory, MemberData(nameof(GetNumberData))]
    public void CanDeserializeNS(Type numberType, string numberText, object numberValue)
    {
        Assert.Equal(
            new object[] { numberValue }, 
            Serializer.DeserializeDynamoDBValue(new() { NS = [numberText] }, typeof(ISet<>).MakeGenericType(numberType)));
    }

    [Fact]
    public void CanDeserializeBS()
    {
        Assert.Equal(
            [[1, 2, 3], [4, 5, 6]],
            Serializer.DeserializeDynamoDBValue<ISet<byte[]>>(new() { BS = [new([1, 2, 3]), new([4, 5, 6])] }));
    }

    [Fact]
    public void CanDeserializeCustomSetFromSS()
    {
        Assert.Equal(
            new CustomSet { "Hello", "World" }, 
            Serializer.DeserializeDynamoDBValue<CustomSet>(new() { SS = ["Hello", "World"] }));
    }

    [Fact]
    public void CanDeserializeCustomSetFromSB()
    {
        Assert.Equal(
            [[1, 2, 3], [4, 5, 6]],
            Serializer.DeserializeDynamoDBValue<ByteArraySet>(new() { BS = [new([1, 2, 3]), new([4, 5, 6])] }));
    }

    [Fact]
    public void CanDeserializeL()
    {
        Assert.Equal(
            [],
            Serializer.DeserializeDynamoDBValue<IList<string>>(new() { IsLSet = true }));
        Assert.Equal(
            ["Hello", "World"],
            Serializer.DeserializeDynamoDBValue<IList<string>>(new() { L = [new() { S = "Hello" }, new() { S = "World" }] }));
    }

    [Fact]
    public void CanDeserializeCustomListFromL()
    {
        Assert.Equal(
            new CustomList(),
            Serializer.DeserializeDynamoDBValue<CustomList>(new() { IsLSet = true }));
        Assert.Equal(
            new CustomList { "Hello", "World" },
            Serializer.DeserializeDynamoDBValue<CustomList>(new() { L = [new() { S = "Hello" }, new() { S = "World" }] }));
    }

    [Fact]
    public void CanDeserializeAsPrimaryKeyFromM()
    {
        Assert.Equal(
            PrimaryKey.ForItem(new PlainObject { Number = 1, Text = "One" }),
            Serializer.DeserializeDynamoDBValue<PrimaryKey<PlainObject>>(new() { M = new() { ["Number"] = new() { N = "1" }, ["Text"] = new() { S = "One" } } }));
    }

    [Fact]
    public void CanDeserializeAsDictionaryFromM()
    {
        Assert.Equal(
            new Dictionary<string, long> { ["One"] = 1, ["Two"] = 2 },
            Serializer.DeserializeDynamoDBValue<IDictionary<string, long>>(new() { M = new() { ["One"] = new() { N = "1" }, ["Two"] = new() { N = "2" } } }));
    }
    
    [Fact]
    public void CanDeserializeAsNonStringKeyedDictionaryFromM()
    {
        Assert.Equal(
            new() { [1] = "One", [2] = "Two" },
            Serializer.DeserializeDynamoDBValue<Dictionary<long, string>>(new() { M = new() { ["1"] = new() { S = "One" }, ["2"] = new() { S = "Two" } } }));
    }

    [Fact]
    public void CanDeserializeAsCustomDictionaryFromM()
    {
        Assert.Equal(
            new CustomDictionary() { ["One"] = 1, ["Two"] = 2 },
            Serializer.DeserializeDynamoDBValue<CustomDictionary>(new() { M = new() { ["One"] = new() { N = "1" }, ["Two"] = new() { N = "2" } } }));
    }

    [Fact]
    public void CanDeserializePlainObjectFromM()
    {
        Assert.Equal(
            new PlainObject { Number = 1, Text = "One" },
            Serializer.DeserializeDynamoDBValue<PlainObject>(new() { M = new() { ["Number"] = new() { N = "1" }, ["Text"] = new() { S = "One" } } }));
    }

    [Fact]
    public void CanDeserializePlainObjectWithFieldsFromM()
    {
        Assert.Equal(
            new PlainObjectWithFields { Number = 1, Text = "One" },
            Serializer.DeserializeDynamoDBValue<PlainObjectWithFields>(new() { M = new() { ["Number"] = new() { N = "1" }, ["Text"] = new() { S = "One" } } }));
    }

    [Fact]
    public void CanDeserializePlainObjectAsBaseTypeFromM()
    {
        Assert.Equal(
            new PlainObject { Number = 1, Text = "One" },
            Serializer.DeserializeDynamoDBValue<object>(new() { M = new() { ["$type"] = new() { S = typeof(PlainObject).AssemblyQualifiedName }, ["Number"] = new() { N = "1" }, ["Text"] = new() { S = "One" } } }));
    }

    [Fact]
    public void CanDeserializePlainObjectWithAttributeOverridesFromM()
    {
        Assert.Equal(
            new Book { Isbn = "1234", Year = new DateOnly(2024, 4, 8), NumberOfPages = 204, Notes = null! },
            Serializer.DeserializeDynamoDBValue<Book>(
                new() 
                { 
                    M = new() 
                    { 
                        ["isbn #"] = new() { S = "1234" }, 
                        ["year published"] = new() { S = "2024-04-08" }, 
                        ["num pages"] = new() { N = "204" }, 
                        ["notes"] = new() { S = "Test" }
                    } 
                }));
    }

    [Fact]
    public void CanDeserializeUntypedFromM()
    {
        Assert.Equal(
            new Dictionary<string, object>() { ["One"] = 1m, ["Two"] = 2m },
            Serializer.DeserializeDynamoDBValue<object>(new() { M = new() { ["One"] = new() { N = "1" }, ["Two"] = new() { N = "2" } } }));
    }

    [Fact]
    public void CanSerializeNULL()
    {
        Assert.True(Serializer.SerializeDynamoDBValue(null, typeof(object)) is { NULL: true });
    }

    [Fact]
    public void CanSerializeBOOL()
    {
        Assert.True(Serializer.SerializeDynamoDBValue(false) is { IsBOOLSet: true, BOOL: false });
        Assert.True(Serializer.SerializeDynamoDBValue(true) is { IsBOOLSet: true, BOOL: true });
    }

    [Fact]
    public void CanSerializeS()
    {
        Assert.True(Serializer.SerializeDynamoDBValue("Hello World") is { S: "Hello World" });
    }

    [Fact]
    public void CanSerializeParsableTypeToS()
    {
        Assert.True(Serializer.SerializeDynamoDBValue(new DateOnly(2024, 3, 31)) is { S: "2024-03-31" });
    }

    [Fact]
    public void CanSerializeCustomParsableTypeToS()
    {
        Assert.True(Serializer.SerializeDynamoDBValue(new CustomParsable('F', 'O', 'O')) is { S: "FOO" });
    }

    [Fact]
    public void CanSerializeEnumTypeToS()
    {
        Assert.True(Serializer.SerializeDynamoDBValue(FileAccess.ReadWrite) is { S: "ReadWrite" });
    }

    [Fact]
    public void CanSerializeEnumTypeWithNameTransformToS()
    {
        options.EnumValueNameTransform = NameTransform.SnakeCase;

        Assert.True(
            Serializer.SerializeDynamoDBValue(AttributeTargets.Parameter | AttributeTargets.ReturnValue | AttributeTargets.GenericParameter) 
                is { S: "parameter, return_value, generic_parameter" });
    }

    [Theory, MemberData(nameof(GetNumberData))]
    public void CanSerializeN(Type numberType, string numberText, object numberValue)
    {
        Assert.True(Serializer.SerializeDynamoDBValue(numberValue, numberType) is { N: var n } && n == numberText);
    }

    [Fact]
    public void CanSerializeEnumTypeToN()
    {
        Assert.True(Serializer.SerializeDynamoDBValue((AttributeTargets)32768) is { N: "32768" });
    }

    [Fact]
    public void CanSerializeEnumTypeWithNameTransformToN()
    {
        options.EnumValueNameTransform = NameTransform.SnakeCase;

        Assert.True(Serializer.SerializeDynamoDBValue((AttributeTargets)32768) is { N: "32768" });
    }

    [Fact]
    public void CanSerializeB()
    {
        Assert.True(
            Serializer.SerializeDynamoDBValue(new byte[] { 1, 2, 3, 4, 5, 6 }) is { B: var b } && 
            b.ToArray() is [1, 2, 3, 4, 5, 6]);
    }

    [Fact]
    public void CanSerializeSS()
    {
        Assert.True(Serializer.SerializeDynamoDBValue(new SortedSet<string>(["Hello", "World"])) is { SS: ["Hello", "World"] });
    }

    [Fact]
    public void CanSerializeParsableTypeToSS()
    {
        Assert.True(Serializer.SerializeDynamoDBValue(new SortedSet<DateOnly>([new DateOnly(2024, 3, 30), new DateOnly(2024, 3, 31)])) is { SS: ["2024-03-30", "2024-03-31"] });
    }

    [Fact]
    public void CanSerializeEnumTypeToSS()
    {
        Assert.True(Serializer.SerializeDynamoDBValue(new SortedSet<FileAccess>([FileAccess.Read, FileAccess.Write])) is { SS: ["Read", "Write"] });
    }

    [Theory, MemberData(nameof(GetNumberData))]
    public void CanSerializeNS(Type numberType, string numberText, object numberValue)
    {
        Assert.True(
            Serializer.SerializeDynamoDBValue(GetNumberSet(numberType, numberValue), typeof(ISet<>).MakeGenericType(numberType)) is { NS: [var ns0] } && 
            ns0 == numberText);
    }

    [Fact]
    public void CanSerializeBS()
    {
        Assert.True(
            Serializer.SerializeDynamoDBValue(new SortedSet<byte[]>([[1, 2, 3], [4, 5, 6]], ByteArrayComparer.Default), typeof(ISet<byte[]>))
                is { BS: var bs } && bs.Select(b => b.ToArray()).ToArray() is [[1, 2, 3], [4, 5, 6]]);
    }

    [Fact]
    public void CanSerializeCustomSetToSS()
    {
        Assert.True(Serializer.SerializeDynamoDBValue(new CustomSet { "Hello", "World" }) is { SS: ["Hello", "World"] });
    }

    [Fact]
    public void CanSerializeCustomSetToBS()
    {
        Assert.True(
            Serializer.SerializeDynamoDBValue(new ByteArraySet([[1, 2, 3], [4, 5, 6]]))
                is { BS: var bs } && bs.Select(b => b.ToArray()).ToArray() is [[1, 2, 3], [4, 5, 6]]);
    }

    [Fact]
    public void CanSerializeL()
    {
        Assert.True(Serializer.SerializeDynamoDBValue(new List<string>()) is { IsLSet: true, L: [] });
        Assert.True(Serializer.SerializeDynamoDBValue(new List<string>(["Hello", "World"])) is { IsLSet: true, L: [{ S: "Hello" }, { S: "World"}] });
    }

    [Fact]
    public void CanSerializeCustomListToL()
    {
        Assert.True(Serializer.SerializeDynamoDBValue(new CustomList()) is { IsLSet: true, L: [] });
        Assert.True(Serializer.SerializeDynamoDBValue(new CustomList { "Hello", "World" }) is { IsLSet: true, L: [{ S: "Hello" }, { S: "World"}] });
    }

    [Fact]
    public void CanSerializePrimaryKeyToM()
    {
        Assert.True(
            Serializer.SerializeDynamoDBValue(PrimaryKey.ForItem(new PlainObject { Number = 1, Text = "One" })) is { IsMSet: true, M: { Count: 2 } attributes } &&
            attributes.GetValueOrDefault("Number") is { N: "1" } && 
            attributes.GetValueOrDefault("Text") is { S: "One" });
    }

    [Fact]
    public void CanSerializeDictionaryToM()
    {
        Assert.True(
            Serializer.SerializeDynamoDBValue(new Dictionary<string, long> { ["One"] = 1, ["Two"] = 2 }) is { IsMSet: true, M: { Count: 2 } attributes } &&
            attributes.GetValueOrDefault("One") is { N: "1" } && 
            attributes.GetValueOrDefault("Two") is { N: "2" });
    }

    [Fact]
    public void CanSerializeCustomDictionaryToM()
    {
        Assert.True(
            Serializer.SerializeDynamoDBValue(new CustomDictionary { ["One"] = 1, ["Two"] = 2 }) is { IsMSet: true, M: { Count: 2 } attributes } &&
            attributes.GetValueOrDefault("One") is { N: "1" } && 
            attributes.GetValueOrDefault("Two") is { N: "2" });
    }
    
    [Fact]
    public void CanSerializeNonStringKeyedDictionaryToM()
    {
        Assert.True(
            Serializer.SerializeDynamoDBValue(new Dictionary<long, string> { [1] = "One", [2] = "Two" }) is { IsMSet: true, M: { Count: 2 } attributes } &&
            attributes.GetValueOrDefault("1") is { S: "One" } && 
            attributes.GetValueOrDefault("2") is { S: "Two" });
    }

    [Fact]
    public void CanSerializePlainObjectToM()
    {
        Assert.True(
            Serializer.SerializeDynamoDBValue(new PlainObject { Number = 1, Text = "One" }) is { IsMSet: true, M: { Count: 2 } attributes } &&
            attributes.GetValueOrDefault("Number") is { N: "1" } && 
            attributes.GetValueOrDefault("Text") is { S: "One" });
    }

    [Fact]
    public void CanSerializePlainObjectWithFieldsToM()
    {
        Assert.True(
            Serializer.SerializeDynamoDBValue(new PlainObjectWithFields { Number = 1, Text = "One" }) is { IsMSet: true, M: { Count: 2 } attributes } &&
            attributes.GetValueOrDefault("Number") is { N: "1" } && 
            attributes.GetValueOrDefault("Text") is { S: "One" });
    }

    [Fact]
    public void CanSerializePlainObjectWithNullValuesIncludedToM()
    {
        options.SerializeNullValues = true;

        Assert.True(
            Serializer.SerializeDynamoDBValue(new PlainObjectWithNullValues { Key = 1 }) is { IsMSet: true, M: { Count: 2 } attributes } &&
            attributes.GetValueOrDefault("Key") is { N: "1" } && 
            attributes.GetValueOrDefault("Text") is { NULL: true });
    }

    [Fact]
    public void CanSerializePlainObjectWithDefaultValuesIncludedToM()
    {
        options.SerializeDefaultValues = true;
        
        Assert.True(
            Serializer.SerializeDynamoDBValue(new PlainObjectWithNullValues { Key = 1 }) is { IsMSet: true, M: { Count: 2 } attributes } &&
            attributes.GetValueOrDefault("Key") is { N: "1" } && 
            attributes.GetValueOrDefault("Number") is { N: "0" });
    }

    [Fact]
    public void CanSerializePlainObjectWithDefaultValuesForTypeIncludedToM()
    {
        options.SerializeDefaultValuesFor = type => type == typeof(long);
        
        Assert.True(
            Serializer.SerializeDynamoDBValue(new PlainObjectWithNullValues { Key = 1 }) is { IsMSet: true, M: { Count: 2 } attributes } &&
            attributes.GetValueOrDefault("Key") is { N: "1" } && 
            attributes.GetValueOrDefault("Number") is { N: "0" });
    }

    [Fact]
    public void CanSerializePlainObjectFromBaseTypeToM()
    {
        Assert.True(
            Serializer.SerializeDynamoDBValue(new PlainObject { Number = 2, Text = "Two" }, typeof(object)) is { IsMSet: true, M: { Count: 3 } attributes } &&
            attributes.GetValueOrDefault("$type") is { S: var typeName } && 
            attributes.GetValueOrDefault("Number") is { N: "2" } && 
            attributes.GetValueOrDefault("Text") is { S: "Two" } && 
            typeName == typeof(PlainObject).AssemblyQualifiedName);
    }

    [Fact]
    public void CanSerializePlainObjectWithAttributeOverridesToM()
    {
        Assert.True(
            Serializer.SerializeDynamoDBValue(new Book { Isbn = "1234", Notes = "Test" }) is { IsMSet: true, M: { Count: 3 } attributes } &&
            attributes.GetValueOrDefault("isbn #") is { S: "1234" } && 
            attributes.GetValueOrDefault("year published") is { NULL: true } && 
            attributes.GetValueOrDefault("num pages") is { N: "0" });
    }

    [Fact]
    public void ThrowsExceptionWhenSerializingFromWriteOnlyProperty()
    {
        Assert.Equal(
            $"Not a readable property or field: 'DynamoDB.Net.Tests.UnitTests.Serialization.DynamoDBSerializerTests+ObjectWithWriteOnlyProperty.WriteOnly'",
            Assert.Throws<DynamoDBSerializationException>(() => 
                Serializer.SerializeDynamoDBValue(new ObjectWithWriteOnlyProperty())).Message);
    }

    [Fact]
    public void ThrowsExceptionWhenDeserializingToReadOnlyProperty()
    {
        Assert.Equal(
            "Not a writable property or field: 'DynamoDB.Net.Tests.UnitTests.Serialization.DynamoDBSerializerTests+ObjectWithReadOnlyProperty.ReadOnly'",
            Assert.Throws<DynamoDBSerializationException>(() => 
                Serializer.DeserializeDynamoDBValue<ObjectWithReadOnlyProperty>(new() { M = new() { ["ReadOnly"] = new() { S = "" } } })).Message);
    }

    [Fact]
    public void DeserializationExceptionContainsPath()
    {
        Assert.Equal(
            "$.Children",
            Assert.Throws<DynamoDBSerializationException>(() => 
                Serializer.DeserializeDynamoDBValue<PlainObjectWithChildren>(
                    new()
                    {
                        M = new() { ["Children"] = new() { S = "X" } }
                    }))
                    .Path);

        Assert.Equal(
            "$.Children[0].Number",
            Assert.Throws<DynamoDBSerializationException>(() => 
                Serializer.DeserializeDynamoDBValue<PlainObjectWithChildren>(
                    new()
                    {
                        M = new() 
                        { 
                            ["Children"] = new() 
                            { 
                                L = [new() { M = new() { ["Number"] = new() { B = new([1, 2, 3]) } } }] 
                            } 
                        }
                    }))
                    .Path);

        Assert.Equal(
            "$.Children[0].Values[\"$X\"]",
            Assert.Throws<DynamoDBSerializationException>(() => 
                Serializer.DeserializeDynamoDBValue<PlainObjectWithChildren>(
                    new()
                    {
                        M = new() 
                        { 
                            ["Children"] = new() 
                            { 
                                L = [new() 
                                { 
                                    M = new() { ["Values"] = new() { M = new() { ["$X"] = new() { B = new([]) } } } } 
                                }] 
                            } 
                        }
                    }))
                    .Path);
    }

    static IEnumerable<object[]> GetNumberData() =>
        [
            [typeof(decimal), "123.456", 123.456M],
            [typeof(byte), "123", (byte)123],
            [typeof(double), "123.456", 123.456],
            [typeof(Half), "123.4", Half.CreateChecked(123.4f)],
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

    static object GetNumberSet(Type numberType, object numberValue)
    {
        var numberSetType = typeof(SortedSet<>).MakeGenericType(numberType);
        var numberSet = Activator.CreateInstance(numberSetType);

        numberSetType.GetMethod(nameof(SortedSet<int>.Add))!.Invoke(numberSet, [numberValue]);

        return numberSet;
    }


    [Table]
    record class PlainObject
    {
        [PartitionKey]
        public int Number { get; set; }

        [SortKey]
        public string? Text { get; set; }
    }

    [Table]
    record class PlainObjectWithFields
    {
        [PartitionKey]
        public int Number;

        [SortKey]
        public string? Text;
    }


    [Table]
    record class PlainObjectWithNullValues
    {
        [PartitionKey]
        public int Key { get; set; }

        public string? Text { get; set; }

        public long Number { get; set; }
    }

    [Table]
    record class PlainObjectWithChildren
    {
        [PartitionKey]
        public int Key { get; set; }

        public string? Text { get; set; }

        public List<ChildObject> Children { get; set; } = [];
    }

    public class ChildObject
    {
        public long Number { get; set; }

        public Dictionary<string, string> Values = [];
    }

    class ObjectWithReadOnlyProperty
    {
        public string ReadOnly => string.Empty; 
    }

    class ObjectWithWriteOnlyProperty
    {
        public string WriteOnly { set { } }
    }

    record class Book
    {
        [DynamoDBProperty(AttributeName = "isbn #")]
        public required string Isbn { get; set; }
        
        [DynamoDBProperty(AttributeName = "year published", SerializeNullValues = true)]
        public DateOnly? Year { get; set; }

        [DynamoDBProperty(AttributeName = "num pages", SerializeDefaultValues = true)]
        public int NumberOfPages { get; set; }

        [DynamoDBProperty(AttributeName = "notes", NotSerialized = true)]
        public required string Notes { get; set; }
    }

    record struct CustomParsable(char A, char B, char C) : IParsable<CustomParsable>, IFormattable
    {
        public static CustomParsable Parse(string s, IFormatProvider? provider)
        {
            if (!TryParse(s, provider, out var result))
                throw new FormatException();

            return result;
        }

        public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out CustomParsable result)
        {
            result = s?.Length == 3 ? new CustomParsable(s[0], s[1], s[2]) : default;
            return result != default;
        }

        public readonly string ToString(string? format, IFormatProvider? formatProvider) => ToString();

        public override readonly string ToString() => new([A, B, C]);
    } 

    class CustomSet : SortedSet<string>
    {
    }

    class CustomList : List<string>
    {
    }

    class CustomDictionary : Dictionary<string, long>
    {
    }
}

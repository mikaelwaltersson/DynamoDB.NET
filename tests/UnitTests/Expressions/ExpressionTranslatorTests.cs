using System.Linq.Expressions;
using DynamoDB.Net.Expressions;
using DynamoDB.Net.Model;
using DynamoDB.Net.Serialization;

using static DynamoDB.Net.DynamoDBExpressions;

namespace DynamoDB.Net.Tests.UnitTests.Expressions;

public class ExpressionTranslatorTests
{
    readonly ExpressionTranslationContext context = new(DynamoDBSerializer.Default);

    [Fact]
    public void CanGetIndexNameFromExpression()
    {
        Assert.Null(ExpressionTranslator.GetIndexName<Item>(item => item.P == 1 && item.S == 2));
        Assert.Equal("local-secondary-index-1", ExpressionTranslator.GetIndexName<Item>(item => item.P == 1 && item.Lsi1 > 2));
        Assert.Equal("local-secondary-index-2", ExpressionTranslator.GetIndexName<Item>(item => item.P == 1 && item.Lsi2 > 2));
        Assert.Equal("global-secondary-index-1", ExpressionTranslator.GetIndexName<Item>(item => item.GsiP == 1 && item.Gsi1S > 2));
        Assert.Equal("global-secondary-index-2", ExpressionTranslator.GetIndexName<Item>(item => item.GsiP == 1 && item.Gsi2S > 2));
        Assert.Equal("global-secondary-index-3", ExpressionTranslator.GetIndexName<Item>(item => item.Gsi3P == 1));
    }

    [Fact]
    public void CanGetIndexNameFromPropertyNames()
    {
        Assert.Null(ExpressionTranslator.GetIndexName<Item>((nameof(Item.P), nameof(Item.S))));
        Assert.Equal("local-secondary-index-1", ExpressionTranslator.GetIndexName<Item>((nameof(Item.P), nameof(Item.Lsi1))));
        Assert.Equal("local-secondary-index-2", ExpressionTranslator.GetIndexName<Item>((nameof(Item.P), nameof(Item.Lsi2))));
        Assert.Equal("global-secondary-index-1", ExpressionTranslator.GetIndexName<Item>((nameof(Item.GsiP), nameof(Item.Gsi1S))));
        Assert.Equal("global-secondary-index-2", ExpressionTranslator.GetIndexName<Item>((nameof(Item.GsiP), nameof(Item.Gsi2S))));
        Assert.Equal("global-secondary-index-3", ExpressionTranslator.GetIndexName<Item>((nameof(Item.Gsi3P), null)));
    }

    [Fact]
    public void CanTranslatePredicateithBinaryOperators()
    {
        Assert.Equal(
            "(X > :v0 AND Y < :v1) OR Z = :v2",  
            ExpressionTranslator.Translate<Item>(item => (item.X > 0 && item.Y < 2) || item.Z == -1, context));
        Assert.True(
            context is { AttributeNames: null, AttributeValues.Count: 3 } &&
            context.AttributeValues[":v0"] is { N: "0" } &&
            context.AttributeValues[":v1"] is { N: "2" } && 
            context.AttributeValues[":v2"] is { N: "-1" });
    }

    [Fact]
    public void CanTranslatePredicateWithUnaryOperator()
    {
        Assert.Equal(
            "NOT B = :v0",  
            ExpressionTranslator.Translate<Item>(item => !item.B, context));
        Assert.True(
            context is { AttributeNames: null, AttributeValues.Count: 1 } &&
            context.AttributeValues[":v0"] is { IsBOOLSet: true, BOOL: true });
    }

    [Fact]
    public void CanTranslatePredicateWithVariableArgumentLengthOperator()
    {
        Assert.Equal(
            ":v0 IN (X, Y)",  
            ExpressionTranslator.Translate<Item>(item => In(42, item.X, item.Y), context));
        Assert.True(
            context is { AttributeNames: null, AttributeValues.Count: 1 } &&
            context.AttributeValues[":v0"] is { N: "42" });
    }

    [Fact]
    public void CanTranslatePredicateWithDynamoDBFunction()
    {
        Assert.Equal(
            "begins_with(A, :v0)",  
            ExpressionTranslator.Translate<Item>(item => BeginsWith(item.A, "Hello "), context));
        Assert.True(
            context is { AttributeNames: null, AttributeValues.Count: 1 } &&
            context.AttributeValues[":v0"] is { S: "Hello " });
    }

    [Fact]
    public void CanTranslatePredicateWithContainsCall()
    {
        Assert.Equal(
            "contains(NumberSet, :v0)",  
            ExpressionTranslator.Translate<Item>(item => item.NumberSet.Contains(123), context));
        Assert.True(
            context is { AttributeNames: null, AttributeValues.Count: 1 } &&
            context.AttributeValues[":v0"] is { N: "123" });
    }

    [Fact]
    public void CanTranslatePredicateWithStartsWithCall()
    {
        Assert.Equal(
            "begins_with(A, :v0)",  
            ExpressionTranslator.Translate<Item>(item => item.A.StartsWith("Hello "), context));
        Assert.True(
            context is { AttributeNames: null, AttributeValues.Count: 1 } &&
            context.AttributeValues[":v0"] is { S: "Hello " });
    }

    [Fact]
    public void CanTranslatePredicateWithCountCall()
    {
        Assert.Equal(
            "size(NumberList) > :v0",  
            ExpressionTranslator.Translate<Item>(item => item.NumberList.Count() > 2, context));
        Assert.True(
            context is { AttributeNames: null, AttributeValues.Count: 1 } &&
            context.AttributeValues[":v0"] is { N: "2" });
    }
    

    [Theory, MemberData(nameof(GetDynamoDBTypeData))]
    public void CanTranslateAttributeTypeMatching(DynamoDBType dynamoDBType, string stringValue)
    {
        Assert.Equal(
            "attribute_type(A, :v0)",  
            ExpressionTranslator.Translate<Item>(item => AttributeType(item.A, dynamoDBType), context));
        Assert.True(
            context is { AttributeNames: null, AttributeValues.Count: 1 } &&
            context.AttributeValues[":v0"] is { S: var s } &&
            s == stringValue);
    }

    [Fact]
    public void CanTranslateIsTypeExpression()
    {
        Assert.Equal(
            "#p0 = :v0",  
            ExpressionTranslator.Translate<object>(item => item is Item, context));
        Assert.True(
            context is { AttributeNames.Count: 1, AttributeValues.Count: 1 } &&
            context.AttributeNames["#p0"] is "$type" &&
            context.AttributeValues[":v0"] is { S: var typeName } &&
            typeName == typeof(Item).AssemblyQualifiedName);
    }

    [Fact]
    public void CanTranslateArrayConstantAsSet()
    {
        Assert.Equal(
            "ADD NumberSet :v0",  
            ExpressionTranslator.Translate<Item>(item => Add(item.NumberSet, new[] { 1, 2, 3 }), context));
        Assert.True(
            context is { AttributeNames: null, AttributeValues.Count: 1 } &&
            context.AttributeValues[":v0"] is { NS: ["1", "2", "3"] });
    }

    [Fact]
    public void CanTranslateArrayConstantAsList()
    {
        Assert.Equal(
            "SET NumberList = list_append(NumberList, :v0)",  
            ExpressionTranslator.Translate<Item>(item => Set(item.NumberList, ListAppend(item.NumberList, new[] { 1, 2, 3 })), context));
        Assert.True(
            context is { AttributeNames: null, AttributeValues.Count: 1 } &&
            context.AttributeValues[":v0"] is { L: [{ N: "1" }, { N: "2" }, { N: "3" }] });
    }

    [Fact]
    public void CanTranslateMemberAccess()
    {
        Assert.Equal(
            "Child.S1 = :v0 AND Child.S2 <> :v0",  
            ExpressionTranslator.Translate<Item>(item => item.Child.S1 == "TEST" && item.Child.S2 != "TEST", context));
        Assert.True(
            context is { AttributeNames: null, AttributeValues.Count: 1 } &&
            context.AttributeValues[":v0"] is { S: "TEST" });
    }

    [Fact]
    public void CanTranslateListAccess()
    {
        Assert.Equal(
            "NumberList[2] < :v0",  
            ExpressionTranslator.Translate<Item>(item => item.NumberList[2] < 4, context));
        Assert.True(
            context is { AttributeNames: null, AttributeValues.Count: 1 } &&
            context.AttributeValues[":v0"] is { N: "4" });
    }

    [Fact]
    public void CanTranslateDictionaryAccess()
    {
        Assert.Equal(
            "Dictionary.TEST > :v0",  
            ExpressionTranslator.Translate<Item>(item => item.Dictionary["TEST"] > 4, context));
        Assert.True(
            context is { AttributeNames: null, AttributeValues.Count: 1 } &&
            context.AttributeValues[":v0"] is { N: "4" });
    }

    [Fact]
    public void CanTranslateDictionaryAccessWithNonValidKeyName()
    {
        Assert.Equal(
            "Dictionary.#p0 > :v0",  
            ExpressionTranslator.Translate<Item>(item => item.Dictionary["T.E.S.T"] > 4, context));
        Assert.True(
            context is { AttributeNames.Count: 1, AttributeValues.Count: 1 } &&
            context.AttributeNames["#p0"] is "T.E.S.T" &&
            context.AttributeValues[":v0"] is { N: "4" });
    }

    [Fact]
    public void TranslatesSetToDefaultAsRemove()
    {
        Assert.Equal(
            "REMOVE B",  
            ExpressionTranslator.Translate<Item>(item => Set(item.B, false), context));
        Assert.True(
            context is { AttributeNames: null, AttributeValues: null });

        Assert.Equal(
            "REMOVE B\nSET B2 = :v0, NumberList[0] = :v1",  
            ExpressionTranslator.Translate<Item>(item => Set(item.B, false) & Set(item.B2, false) & Set(item.NumberList[0], 1), context));
        Assert.True(
            context is { AttributeNames: null, AttributeValues.Count: 2 } &&
            context.AttributeValues[":v0"] is { IsBOOLSet: true, BOOL: false } &&
            context.AttributeValues[":v1"] is { N: "1" });
    }

    [Fact]
    public void TranslatesSetToNullAsRemove()
    {
        Assert.Equal(
            "REMOVE C, Child.S2\nSET C2 = :v0",  
            ExpressionTranslator.Translate<Item>(item => Set(item.C, null) & Set(item.C2, null) & Set(item.Child.S2, null), context));
        Assert.True(
            context is { AttributeNames: null, AttributeValues.Count: 1 } &&
            context.AttributeValues[":v0"] is { NULL: true });
    }

    [Fact]
    public void CanTranslateRawExpression()
    {
        Assert.Equal(
            "begins_with(#p0, :v0)",  
            ExpressionTranslator.Translate(
                (Expression<Func<Item, bool>>)new RawExpression<Item>("begins_with(#p0, :v0)") 
                { 
                    Names = { ["#p0"] = "A" },
                    Values = { [":v0"] = "Hello " } 
                },
                context));
        Assert.True(
            context is { AttributeNames.Count: 1, AttributeValues.Count: 1 } &&
            context.AttributeNames["#p0"] is "A" &&
            context.AttributeValues[":v0"] is { S: "Hello " });
    }



    static IEnumerable<object[]> GetDynamoDBTypeData() =>
        [
            [DynamoDBType.Null, "NULL"],
            [DynamoDBType.Bool, "BOOL"],
            [DynamoDBType.String, "S"],
            [DynamoDBType.Number, "N"],
            [DynamoDBType.Binary, "B"],
            [DynamoDBType.StringSet, "SS"],
            [DynamoDBType.NumberSet, "NS"],
            [DynamoDBType.BinarySet, "BS"],
            [DynamoDBType.List, "L"], 
            [DynamoDBType.Map, "M"]
        ];

    [Table]
    class Item
    {
        [PartitionKey]
        public int P { get; set; }

        [SortKey]
        public int S { get; set; }

        [SortKey(LocalSecondaryIndex = 1, IndexName = "local-secondary-index-1")]
        public int Lsi1 { get; set; }

        [SortKey(LocalSecondaryIndex = 2, IndexName = "local-secondary-index-2")]
        public int Lsi2 { get; set; }

        [PartitionKey(GlobalSecondaryIndex = 1, IndexName = "global-secondary-index-1")]
        [PartitionKey(GlobalSecondaryIndex = 2, IndexName = "global-secondary-index-2")]
        public int GsiP { get; set; }
    
        [SortKey(GlobalSecondaryIndex = 1)]
        public int Gsi1S { get; set; }

        [SortKey(GlobalSecondaryIndex = 2)]
        public int Gsi2S { get; set; }
    
        [PartitionKey(GlobalSecondaryIndex = 3, IndexName = "global-secondary-index-3")]
        public int Gsi3P { get; set; }

        public int X { get; set; }

        public int Y { get; set; }

        public int Z { get; set; }

        public string A { get; set; } = string.Empty;

        public bool B { get; set; }

        [DynamoDBProperty(SerializeDefaultValues = true)]
        public bool B2 { get; set; }

        public string? C { get; set; }

        [DynamoDBProperty(SerializeNullValues = true)]
        public string? C2 { get; set; }

        public SortedSet<int> NumberSet { get; set; } = [];

        public List<int> NumberList { get; set; } = [];

        public Dictionary<string, int> Dictionary { get; set; } = [];

        public ChildItem Child { get; set; }= new();

        public PrimaryKey<ChildItem> Child2 { get; set; }
    }

    class ChildItem
    {
        public string S1 { get; set; } = string.Empty;
        
        public string? S2 { get; set; }
    }
}

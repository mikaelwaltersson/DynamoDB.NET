using Amazon.DynamoDBv2.Model;
using DynamoDB.Net.Serialization;

namespace DynamoDB.Net.Tests.UnitTests.Serialization;

public class AttributeValueComparerTests
{
    [Theory, MemberData(nameof(GetCompareEqualAttributeValues))]
    public void CanCompareEqualAttributeValues(AttributeValue x, AttributeValue y)
    {
        Assert.True(AttributeValueComparer.Default.Equals(x, y));
        Assert.True(AttributeValueComparer.Default.GetHashCode(x) == AttributeValueComparer.Default.GetHashCode(x));
    }

    [Theory, MemberData(nameof(GetCompareNonEqualAttributeValuesTestData))]
    public void CanCompareNonEqualAttributeValues(AttributeValue x, AttributeValue y)
    {
        Assert.False(AttributeValueComparer.Default.Equals(x, y));
    }

    static IEnumerable<AttributeValue[]>  GetCompareEqualAttributeValues() =>
        [
            [new(), new()],
            [new() { NULL = true }, new() { NULL = true }],
            [new() { IsBOOLSet = true }, new() { IsBOOLSet = true }],
            [new() { S = "string" }, new() { S = "string" }],
            [new() { N = "123" }, new() { N = "123" }],
            [new() { B = new([1, 2, 3]) }, new() { B = new([1, 2, 3]) }],
            [new() { SS = ["string"] }, new() { SS = ["string"] }],
            [new() { NS = ["123"] }, new() { NS = ["123"] }],
            [new() { BS = [new([1, 2, 3])] }, new() { BS = [new([1, 2, 3])] }],
            [new() { L = [new() { S = "string" }] }, new() { L = [new() { S = "string" }] }],
            [new() { M = new() { ["p1"] = new() { N = "123" } } }, new() { M = new() { ["p1"] = new() { N = "123" } } }]
        ];

    static IEnumerable<AttributeValue[]> GetCompareNonEqualAttributeValuesTestData() =>
        GetCompareEqualAttributeValues().Zip(
            GetCompareEqualAttributeValues().Skip(1).Append(GetCompareEqualAttributeValues().First()), 
            (x, y) => new[] { x[0], y[0] });
}

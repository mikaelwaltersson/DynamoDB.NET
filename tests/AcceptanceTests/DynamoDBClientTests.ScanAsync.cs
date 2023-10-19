namespace DynamoDB.Net.Tests.AcceptanceTests;

using Amazon.DynamoDBv2.Model;

public partial class DynamoDBClientTests
{
    [Fact]
    public async Task ScanAsyncReturnsObjects()
    {
        for (var i = 0; i < 3; i++)
        {
            await dynamoDB.PutItemAsync(
                tableName, 
                new Dictionary<string, AttributeValue>
                {
                    ["userId"] = new AttributeValue { S = $"00000000-0000-0000-0000-00000000000{1 + i}" },
                    ["timestamp"] = new AttributeValue { S = $"2022-10-18T16:4{2 + i}Z" },
                    ["roleIds"] = new AttributeValue 
                    {
                        SS =
                        {
                            $"00000000-0000-0000-0000-10000000000{1 + i}",
                            $"00000000-0000-0000-0000-10000000000{2 + i}"
                        }
                    }
                });
        }

        var items = await dynamoDBClient.ScanAsync<TestModels.UserPost>();

        Assert.Equal(3, items.Count);
        Snapshot.Match(items);
    }
}

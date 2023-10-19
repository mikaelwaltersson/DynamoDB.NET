namespace DynamoDB.Net.Tests.AcceptanceTests;

using Amazon.DynamoDBv2.Model;

public partial class DynamoDBClientTests
{
    [Fact]
    public async Task TryGetAsyncReturnsObject()
    {
        await dynamoDB.PutItemAsync(
            tableName, 
            new Dictionary<string, AttributeValue>
            {
                ["userId"] = new AttributeValue { S = "00000000-0000-0000-0000-000000000001" },
                ["timestamp"] = new AttributeValue { S = "2022-10-18T16:42Z" },
                ["roleIds"] = new AttributeValue 
                {
                    SS =
                    {
                        "00000000-0000-0000-0000-100000000001",
                        "00000000-0000-0000-0000-100000000002"
                    }
                }
            });
        
        var item = 
            await dynamoDBClient.TryGetAsync(
                new PrimaryKey<TestModels.UserPost>(
                    new Guid("00000000-0000-0000-0000-000000000001"), 
                    new DateTime(2022, 10, 18, 16, 42, 0, DateTimeKind.Utc)));

        Assert.NotNull(item);
        Snapshot.Match(item);
    }

    [Fact]
    public async Task TryGetAsyncReturnsNullForNonExistingKey()
    {
        var item = 
            await dynamoDBClient.TryGetAsync(
                new PrimaryKey<TestModels.UserPost>(
                    new Guid("00000000-0000-0000-0000-000000000001"), 
                    new DateTime(2022, 10, 18, 16, 42, 0, DateTimeKind.Utc)));

        Assert.Null(item);
    }
}

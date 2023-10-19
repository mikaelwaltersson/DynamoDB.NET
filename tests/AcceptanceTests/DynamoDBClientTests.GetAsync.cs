namespace DynamoDB.Net.Tests.AcceptanceTests;

using Amazon.DynamoDBv2.Model;
using DynamoDB.Net.Exceptions;

public partial class DynamoDBClientTests
{
    [Fact]
    public async Task GetAsyncReturnsObject()
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
            await dynamoDBClient.GetAsync(
                new PrimaryKey<TestModels.UserPost>(
                    new Guid("00000000-0000-0000-0000-000000000001"), 
                    new DateTime(2022, 10, 18, 16, 42, 0, DateTimeKind.Utc)));

        Assert.NotNull(item);
        Snapshot.Match(item);
    }

    [Fact]
    public async Task GetAsyncThrowsErrorForNonExistingKey()
    {
        await Assert.ThrowsAsync<ItemNotFoundException<TestModels.UserPost>>(async () => 
            await dynamoDBClient.GetAsync(
                new PrimaryKey<TestModels.UserPost>(
                    new Guid("00000000-0000-0000-0000-000000000001"), 
                    new DateTime(2022, 10, 18, 16, 42, 0, DateTimeKind.Utc))));
    }
}

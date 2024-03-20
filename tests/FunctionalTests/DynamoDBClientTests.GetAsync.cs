using Amazon.DynamoDBv2.Model;

namespace DynamoDB.Net.Tests.FunctionalTests;

public partial class DynamoDBClientTests
{
    [Fact]
    public async Task GetAsyncReturnsItem()
    {
        // Arrange
        await dynamoDB.PutItemAsync(
            tableName, 
            new Dictionary<string, AttributeValue>
            {
                ["userId"] = new() { S = "f4ebd04f-0bd7-43b9-90a8-d6de295927f3" },
                ["timestamp"] = new() { S = "2022-10-18T16:42:00.0000000+00:00" },
                ["linkedPostIds"] = new() 
                {
                    L =
                    {
                        new() { S = "aa5988f8-4a3f-40ae-987b-22bac870b170" },
                        new() { S = "14598640-6c86-4206-b1a6-753e640b008a" }
                    }
                },
                ["version"] = new() { N = "1" }
            });
        
        // Act
        var item = 
            await dynamoDBClient.GetAsync<TestModels.UserPost>(
                (new Guid("f4ebd04f-0bd7-43b9-90a8-d6de295927f3"), new DateTimeOffset(2022, 10, 18, 16, 42, 0, TimeSpan.Zero)));

        // Assert
        Assert.NotNull(item);
        Snapshot.Match(item);
    }

    [Fact]
    public async Task GetAsyncThrowsErrorForNonExistingKey()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ItemNotFoundException<TestModels.UserPost>>(() => 
            dynamoDBClient.GetAsync<TestModels.UserPost>(
                (new Guid("bbb8dfe9-650f-4cd1-abc0-354de2b71a6d"), new DateTimeOffset(2022, 10, 18, 16, 42, 0, TimeSpan.Zero))));
    }
}

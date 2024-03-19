using Amazon.DynamoDBv2.Model;

namespace DynamoDB.Net.Tests.AcceptanceTests;

public partial class DynamoDBClientTests
{
    [Fact]
    public async Task TryGetAsyncReturnsItems()
    {
        // Arrange
        await dynamoDB.PutItemAsync(
            tableName, 
            new Dictionary<string, AttributeValue>
            {
                ["userId"] = new AttributeValue { S = "4793561c-e693-4e58-b31f-99bd9df840ab" },
                ["timestamp"] = new AttributeValue { S = "2022-10-18T16:42:00.0000000+00:00" },
                ["roleIds"] = new AttributeValue 
                {
                    L =
                    {
                        new AttributeValue { S = "3d023a08-ed04-4bd5-b9c2-32c11a7b8199" },
                        new AttributeValue { S = "aef0a60f-f5e5-416c-bfa4-9d7c2f66d207" }
                    }
                }
            });
        
        // Act
        var item = 
            await dynamoDBClient.TryGetAsync<TestModels.UserPost>(
                (new Guid("4793561c-e693-4e58-b31f-99bd9df840ab"), new DateTimeOffset(2022, 10, 18, 16, 42, 0, TimeSpan.Zero)));

        // Assert
        Assert.NotNull(item);
        Snapshot.Match(item);
    }

    [Fact]
    public async Task TryGetAsyncReturnsNullForNonExistingKey()
    {
        // Act
        var item = 
            await dynamoDBClient.TryGetAsync<TestModels.UserPost>(
                (new Guid("00000000-0000-0000-0000-000000000001"), new DateTimeOffset(2022, 10, 18, 16, 42, 0, TimeSpan.Zero)));

        // Assert
        Assert.Null(item);
    }
}

using Amazon.DynamoDBv2.Model;

namespace DynamoDB.Net.Tests.AcceptanceTests;

public partial class DynamoDBClientTests
{
    [Fact]
    public async Task QueryAsyncReturnsObjects()
    {
        // Arrange
        for (var i = 0; i < 4; i++)
        {
            await dynamoDB.PutItemAsync(
                tableName, 
                new Dictionary<string, AttributeValue>
                {
                    ["userId"] = new AttributeValue { S = $"d98e04ab-7920-4296-a0de-{1 + (i / 2):D12}" },
                    ["timestamp"] = new AttributeValue { S = $"2022-10-18T16:{1 + i:D2}:00Z" },
                    ["roleIds"] = new AttributeValue 
                    {
                        L =
                        {
                            new AttributeValue { S = $"bc91a88b-e98c-4da7-85b0-{1 + i:D12}" },
                            new AttributeValue { S = $"59e6b58d-6678-4a71-b14b-{1 + i:D12}" }
                        }
                    }
                });
        }

        // Act
        var items = await dynamoDBClient.QueryAsync<TestModels.UserPost>(userPost => userPost.UserId == new Guid("d98e04ab-7920-4296-a0de-000000000002"));

        // Assert
        Assert.Equal(2, items.Count);
        Snapshot.Match(items);
    }
}

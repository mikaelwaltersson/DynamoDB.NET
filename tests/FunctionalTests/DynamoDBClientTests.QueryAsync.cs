using Amazon.DynamoDBv2.Model;
using static DynamoDB.Net.DynamoDBExpressions;

namespace DynamoDB.Net.Tests.FunctionalTests;

public partial class DynamoDBClientTests
{
    [Fact]
    public async Task QueryAsyncReturnsItems()
    {
        // Arrange
        for (var i = 0; i < 4; i++)
        {
            await dynamoDB.PutItemAsync(
                tableName,
                new Dictionary<string, AttributeValue>
                {
                    ["userId"] = new() { S = $"d98e04ab-7920-4296-a0de-{1 + (i / 2):D12}" },
                    ["timestamp"] = new() { S = $"2022-10-18T16:{1 + i:D2}:00.0000000+00:00" },
                    ["linkedPostIds"] = new() 
                    {
                        L =
                        {
                            new() { S = $"bc91a88b-e98c-4da7-85b0-{1 + i:D12}" },
                            new() { S = $"59e6b58d-6678-4a71-b14b-{1 + i:D12}" }
                        }
                    },
                    ["priority"] = new() { N = $"{1 + (i % 2)}" },
                    ["tags"] = new() { SS = ["data", "test"] },
                    ["externalId"] = new() { S = $"ID_{1 + i}" },  
                    ["version"] = new() { N = "1" }
                });
        }

        // Act
        var items = 
            await dynamoDBClient.QueryAsync<TestModels.UserPost>(
                userPost => userPost.UserId == new Guid("d98e04ab-7920-4296-a0de-000000000002"), 
                filter: userPost => Contains(userPost.Tags, "test"));

        var itemsByGlobalSecondaryIndex = 
            await dynamoDBClient.QueryAsync<TestModels.UserPost>(userPost => userPost.ExternalId == "ID_3");

        var itemsByLocalSecondaryIndex = 
            await dynamoDBClient.QueryAsync<TestModels.UserPost>(
                userPost => 
                    userPost.UserId == new Guid("d98e04ab-7920-4296-a0de-000000000001") && 
                    userPost.Priority >= 2);

        // Assert
        Assert.Equal(2, items.Count);
        Assert.Equal(1, itemsByGlobalSecondaryIndex.Count);
        Assert.Equal(1, itemsByLocalSecondaryIndex.Count);
        Snapshot.Match(new { items, itemsByGlobalSecondaryIndex, itemsByLocalSecondaryIndex });
    }
}

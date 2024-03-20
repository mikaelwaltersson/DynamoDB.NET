namespace DynamoDB.Net.Tests.FunctionalTests;

using Amazon.DynamoDBv2.Model;
using static DynamoDB.Net.DynamoDBExpressions;

public partial class DynamoDBClientTests
{
    [Fact]
    public async Task WriteTransactionsExecutesAllOperations()
    {
        // Arrange
        for (var i = 0; i < 3; i++)
        {
            await dynamoDB.PutItemAsync(
                tableName, 
                new Dictionary<string, AttributeValue>
                {
                    ["userId"] = new AttributeValue { S = $"40822a39-1d07-4170-9486-{1 + i:D12}" },
                    ["timestamp"] = new AttributeValue { S = $"2022-10-18T16:{1 + i:D2}:00.0000000+00:00" },
                    ["linkedPostIds"] = new AttributeValue 
                    {
                        L =
                        {
                            new AttributeValue { S = $"4bd6e5ff-3f77-4895-b861-{1 + i:D12}" },
                            new AttributeValue { S = $"34f9d49a-706d-4bed-b9e8-{1 + i:D12}" }
                        }
                    },
                    ["version"] = new() { N = "1" }
                });
        }

        var newUserPost = 
            new TestModels.UserPost
            {
                UserId = new Guid("40822a39-1d07-4170-9486-000000000004"),
                Timestamp = new DateTimeOffset(2022, 10, 18, 16, 4, 0, TimeSpan.Zero),
                LinkedPostIds =
                { 
                    new Guid("4bd6e5ff-3f77-4895-b861-000000000004"),
                    new Guid("34f9d49a-706d-4bed-b9e8-000000000004")
                }
            };

        var existingUserPost1 = 
            new TestModels.UserPost
            {
                UserId = new Guid("40822a39-1d07-4170-9486-000000000001"), 
                Timestamp = new DateTime(2022, 10, 18, 16, 1, 0, DateTimeKind.Utc),
            };

        var existingUserPost2 = 
            new TestModels.UserPost 
            { 
                UserId = new Guid("40822a39-1d07-4170-9486-000000000002"), 
                Timestamp = new DateTimeOffset(2022, 10, 18, 16, 2, 0, TimeSpan.Zero)
            };

        var existingUserPost3 = 
            new TestModels.UserPost
            {
                UserId = new Guid("40822a39-1d07-4170-9486-000000000003"), 
                Timestamp = new DateTimeOffset(2022, 10, 18, 16, 3, 0, TimeSpan.Zero)
            };

        // Act
        var transaction = dynamoDBClient.BeginWriteTransaction();

        transaction.Put(newUserPost, condition: () => AttributeNotExists(newUserPost.UserId));

        transaction.Update(
            existingUserPost1,
            () =>
                Set(existingUserPost1.LinkedPostIds,
                    ListAppend(
                        existingUserPost1.LinkedPostIds, 
                        new List<Guid> 
                        {
                            new("12d73fb1-5286-4376-b3b5-e03eb6a4ec13"),
                            new("8f671114-c522-4a5b-9490-0165a1b8e56e")
                        })),
            version: 1);

        transaction.Delete(existingUserPost2, version: 1);

        transaction.ConditionCheck(existingUserPost3, () => Size(existingUserPost3.LinkedPostIds) > 1);

        await transaction.CommitAsync();

        // Assert
        var items = 
            (await dynamoDB.ScanAsync(
                new ScanRequest 
                { 
                    TableName = tableName 
                })).Items;

        Assert.Equal(3, items.Count);
        Snapshot.Match(items);
    } 
}


using Amazon.DynamoDBv2.Model;
using static DynamoDB.Net.DynamoDBExpressions;

namespace DynamoDB.Net.Tests.FunctionalTests;

public partial class DynamoDBClientTests
{
    [Fact]
    public async Task UpdateAsyncStorePartialChangesToItem()
    {
        // Arrange
        await dynamoDB.PutItemAsync(
            tableName, 
            new Dictionary<string, AttributeValue>
            {
                ["userId"] = new AttributeValue { S = "550e83bf-5117-4b85-a145-bcca4742cb6c" },
                ["timestamp"] = new AttributeValue { S = "2022-10-18T16:42:00.0000000+00:00" },
                ["linkedPostIds"] = new AttributeValue 
                {
                    L =
                    {
                        new AttributeValue { S = "bfd107fc-7777-4208-8d41-4072c0878e9a" },
                        new AttributeValue { S = "0cd085ee-3285-4eed-be74-8f1c8b575334" }
                    }
                },
                ["version"] = new() { N = "1" }
            });
        
        // Act
        var item = 
            await dynamoDBClient.UpdateAsync<TestModels.UserPost>(
                (new Guid("550e83bf-5117-4b85-a145-bcca4742cb6c"), new DateTimeOffset(2022, 10, 18, 16, 42, 0, TimeSpan.Zero)),
                userPost =>
                    Set(userPost.LinkedPostIds, 
                        ListAppend(
                            userPost.LinkedPostIds, 
                            new List<Guid> 
                            {
                                new("530ee358-ef36-4314-9e3a-4e63250917e5"),
                                new("729061bf-eac8-492f-9d5a-b9fe7bd5496c")
                            })),
                version: 1);

        // Assert
        Assert.NotNull(item);
        Snapshot.Match(item);
    }

    [Fact]
    public async Task UpdateAsyncWithConditionStorePartialChangesToItem()
    {
        // Arrange
        await dynamoDB.PutItemAsync(
            tableName, 
            new Dictionary<string, AttributeValue>
            {
                ["userId"] = new AttributeValue { S = "550e83bf-5117-4b85-a145-bcca4742cb6c" },
                ["timestamp"] = new AttributeValue { S = "2022-10-19T16:42:00.0000000+00:00" },
                ["linkedPostIds"] = new AttributeValue 
                {
                    L =
                    {
                        new AttributeValue { S = "bfd107fc-7777-4208-8d41-4072c0878e9a" },
                        new AttributeValue { S = "0cd085ee-3285-4eed-be74-8f1c8b575334" }
                    }
                },
                ["version"] = new() { N = "1" }
            });

        var item = 
            new TestModels.UserPost 
            { 
                UserId = new Guid("550e83bf-5117-4b85-a145-bcca4742cb6c"),
                Timestamp = new DateTimeOffset(2022, 10, 19, 16, 42, 0, TimeSpan.Zero)
            };

        // Act
        item = 
            await dynamoDBClient.UpdateAsync(
                item,
                update: () =>
                    Set(item.LinkedPostIds, 
                        ListAppend(
                            item.LinkedPostIds, 
                            new List<Guid> 
                            {
                                new("530ee358-ef36-4314-9e3a-4e63250917e5"),
                                new("729061bf-eac8-492f-9d5a-b9fe7bd5496c")
                            })),
                condition: () => 
                    Contains(item.LinkedPostIds, new Guid("bfd107fc-7777-4208-8d41-4072c0878e9a")),
                version: 1);

        // Assert
        Assert.NotNull(item);
        Snapshot.Match(item);
    }
}

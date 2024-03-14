using Amazon.DynamoDBv2.Model;
using static DynamoDB.Net.DynamoDBExpressions;

namespace DynamoDB.Net.Tests.AcceptanceTests;

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
                ["timestamp"] = new AttributeValue { S = "2022-10-18T16:42Z" },
                ["roleIds"] = new AttributeValue 
                {
                    L =
                    {
                        new AttributeValue { S = "bfd107fc-7777-4208-8d41-4072c0878e9a" },
                        new AttributeValue { S = "0cd085ee-3285-4eed-be74-8f1c8b575334" }
                    }
                }
            });
        
        // Act
        var item = 
            await dynamoDBClient.UpdateAsync(
                new PrimaryKey<TestModels.UserPost>(
                    new Guid("550e83bf-5117-4b85-a145-bcca4742cb6c"), 
                    new DateTime(2022, 10, 18, 16, 42, 0, DateTimeKind.Utc)),
                userPost =>
                    Set(userPost.RoleIds, 
                        ListAppend(
                            userPost.RoleIds, 
                            new[] 
                            {
                                new Guid("530ee358-ef36-4314-9e3a-4e63250917e5"),
                                new Guid("729061bf-eac8-492f-9d5a-b9fe7bd5496c")
                            })));

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
                ["timestamp"] = new AttributeValue { S = "2022-10-19T16:42Z" },
                ["roleIds"] = new AttributeValue 
                {
                    L =
                    {
                        new AttributeValue { S = "bfd107fc-7777-4208-8d41-4072c0878e9a" },
                        new AttributeValue { S = "0cd085ee-3285-4eed-be74-8f1c8b575334" }
                    }
                }
            });

        var item = 
            new TestModels.UserPost 
            { 
                UserId = new Guid("550e83bf-5117-4b85-a145-bcca4742cb6c"),
                Timestamp = new DateTime(2022, 10, 19, 16, 42, 0, DateTimeKind.Utc)
            };

        // Act
        item = 
            await dynamoDBClient.UpdateAsync(
                item,
                update: () =>
                    Set(item.RoleIds, 
                        ListAppend(
                            item.RoleIds, 
                            new[] 
                            {
                                new Guid("530ee358-ef36-4314-9e3a-4e63250917e5"),
                                new Guid("729061bf-eac8-492f-9d5a-b9fe7bd5496c")
                            })),
                condition: () => 
                    Contains(item.RoleIds, new Guid("bfd107fc-7777-4208-8d41-4072c0878e9a")));

        // Assert
        Assert.NotNull(item);
        Snapshot.Match(item);
    }
}

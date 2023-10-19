namespace DynamoDB.Net.Tests.AcceptanceTests;

using Amazon.DynamoDBv2.Model;
using static DynamoDB.Net.DynamoDBExpressions;

public partial class DynamoDBClientTests
{
    [Fact]
    public async Task UpdateAsyncStorePartialChanges()
    {
        await dynamoDB.PutItemAsync(
            tableName, 
            new Dictionary<string, AttributeValue>
            {
                ["userId"] = new AttributeValue { S = "00000000-0000-0000-0000-000000000001" },
                ["timestamp"] = new AttributeValue { S = "2022-10-18T16:42Z" },
                ["roleIds"] = new AttributeValue 
                {
                    L =
                    {
                        new AttributeValue { S = "00000000-0000-0000-0000-100000000001" },
                        new AttributeValue { S = "00000000-0000-0000-0000-100000000002" }
                    }
                }
            });
        
        var item = 
            await dynamoDBClient.UpdateAsync(
                new PrimaryKey<TestModels.UserPost>(
                    new Guid("00000000-0000-0000-0000-000000000001"), 
                    new DateTime(2022, 10, 18, 16, 42, 0, DateTimeKind.Utc)),
                userPost =>
                    Set(userPost.RoleIds, 
                        ListAppend(
                            userPost.RoleIds, 
                            new[] 
                            {
                                new Guid("00000000-0000-0000-0000-000000000004"),
                                new Guid("00000000-0000-0000-0000-000000000005")
                            })));

        Assert.NotNull(item);
        Snapshot.Match(item);
    }
}

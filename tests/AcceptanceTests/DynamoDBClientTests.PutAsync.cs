using Amazon.DynamoDBv2.Model;

namespace DynamoDB.Net.Tests.AcceptanceTests;

public partial class DynamoDBClientTests
{
    [Fact]
    public async Task PutAsyncStoresObject()
    {
        // Act
        await dynamoDBClient.PutAsync(
            new TestModels.UserPost
            {
                UserId = new Guid("65111529-2dbf-4f49-85d4-f0221035f9d5"),
                Timestamp = new DateTime(2022, 10, 18, 16, 42, 0, DateTimeKind.Utc),
                RoleIds =
                { 
                    new Guid("cbaf96a2-8c1d-46b1-952c-59596145b158"),
                    new Guid("585381ad-785e-4bc9-9c85-da8a0b754dc6")
                }
            });

        // Assert
        var item = 
            (await dynamoDB.GetItemAsync(
                tableName, 
                new Dictionary<string, AttributeValue>
                {
                    ["userId"] = new AttributeValue { S = "65111529-2dbf-4f49-85d4-f0221035f9d5" },
                    ["timestamp"] = new AttributeValue { S = "2022-10-18T16:42Z" }
                })).Item;

        Assert.NotNull(item);
        Snapshot.Match(item);
    }
}

using Amazon.DynamoDBv2.Model;
using Snapshooter.Xunit;
using Xunit;

namespace DynamoDB.Net.Tests.AcceptanceTests
{
    public partial class DynamoDBClientTests
    {
        [Fact]
        public async Task PutAsyncStoresObject()
        {
            await dynamoDBClient.PutAsync(
                new TestModels.UserPost
                {
                    UserId = new Guid("00000000-0000-0000-0000-000000000001"),
                    Timestamp = new DateTime(2022, 10, 18, 16, 42, 0, DateTimeKind.Utc),
                    RoleIds =
                    { 
                        new Guid("00000000-0000-0000-0000-100000000001"),
                        new Guid("00000000-0000-0000-0000-100000000001")
                    }
                });
            
            var item = 
                (await dynamoDB.GetItemAsync(
                    tableName, 
                    new Dictionary<string, AttributeValue>
                    {
                        ["userId"] = new AttributeValue { S = "00000000-0000-0000-0000-000000000001" },
                        ["timestamp"] = new AttributeValue { S = "2022-10-18T16:42Z" }
                    })).Item;

            Assert.NotNull(item);
            Snapshot.Match(item);
        }
    }
}
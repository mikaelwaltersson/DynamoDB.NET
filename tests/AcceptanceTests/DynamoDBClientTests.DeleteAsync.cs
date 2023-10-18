using Amazon.DynamoDBv2.Model;
using DynamoDB.Net;
using DynamoDB.Net.Serialization;
using Snapshooter.Xunit;
using Xunit;

namespace DynamoDB.Net.Tests.AcceptanceTests
{
    public partial class DynamoDBClientTests
    {
        [Fact]
        public async Task DeleteAsyncReturnsObject()
        {
            await dynamoDB.PutItemAsync(
                tableName, 
                new Dictionary<string, AttributeValue>
                {
                    ["userId"] = new AttributeValue { S = "00000000-0000-0000-0000-000000000001" },
                    ["timestamp"] = new AttributeValue { S = "2022-10-18T16:42Z" },
                    ["roleIds"] = new AttributeValue 
                    {
                        SS =
                        {
                            "00000000-0000-0000-0000-100000000001",
                            "00000000-0000-0000-0000-100000000002"
                        }
                    }
                });
            
            await dynamoDBClient.DeleteAsync(
                new PrimaryKey<TestModels.UserPost>(
                    new Guid("00000000-0000-0000-0000-000000000001"), 
                    new DateTime(2022, 10, 18, 16, 42, 0, DateTimeKind.Utc)));

            Assert.Empty(
                (await dynamoDB.GetItemAsync(
                    tableName, 
                    new Dictionary<string, AttributeValue> 
                    {
                        ["userId"] = new AttributeValue { S = "00000000-0000-0000-0000-000000000001" },
                        ["timestamp"] = new AttributeValue { S = "2022-10-18T16:42Z" },    
                    })).Item);
        }

        [Fact]
        public async Task DeleteAsyncIsNoOpForNonExistingKey()
        {
            await dynamoDBClient.DeleteAsync(
                new PrimaryKey<TestModels.UserPost>(
                    new Guid("00000000-0000-0000-0000-000000000001"), 
                    new DateTime(2022, 10, 18, 16, 42, 0, DateTimeKind.Utc)));
        }       
    }
}
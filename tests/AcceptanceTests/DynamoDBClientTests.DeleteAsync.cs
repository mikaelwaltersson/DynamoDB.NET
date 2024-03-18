using Amazon.DynamoDBv2.Model;
using static DynamoDB.Net.DynamoDBExpressions;

namespace DynamoDB.Net.Tests.AcceptanceTests;

public partial class DynamoDBClientTests
{
    [Fact]
    public async Task DeleteAsyncRemovesItem()
    {
        // Arrange
        await dynamoDB.PutItemAsync(
            tableName, 
            new Dictionary<string, AttributeValue>
            {
                ["userId"] = new AttributeValue { S = "a684e596-2f85-4259-8108-55eff1e4acce" },
                ["timestamp"] = new AttributeValue { S = "2022-10-18T16:42Z" },
                ["roleIds"] = new AttributeValue 
                {
                    L =
                    {
                        new AttributeValue { S = "b2a3fbf8-fa90-404b-aaca-a1a604bbce6f" },
                        new AttributeValue { S = "c615432a-6b3a-4570-aa3b-18ef77278024" }
                    }
                }
            });

        // Act
        await dynamoDBClient.DeleteAsync<TestModels.UserPost>(
            (new Guid("a684e596-2f85-4259-8108-55eff1e4acce"), new DateTime(2022, 10, 18, 16, 42, 0, DateTimeKind.Utc)));

        // Assert
        Assert.Empty(
            (await dynamoDB.GetItemAsync(
                tableName, 
                new Dictionary<string, AttributeValue> 
                {
                    ["userId"] = new AttributeValue { S = "a684e596-2f85-4259-8108-55eff1e4acce" },
                    ["timestamp"] = new AttributeValue { S = "2022-10-18T16:42Z" },    
                })).Item);
    }

    [Fact]
    public async Task DeleteAsyncWithConditionRemovesItem()
    {
        // Arrange
        await dynamoDB.PutItemAsync(
            tableName, 
            new Dictionary<string, AttributeValue>
            {
                ["userId"] = new AttributeValue { S = "a684e596-2f85-4259-8108-55eff1e4acce" },
                ["timestamp"] = new AttributeValue { S = "2022-10-19T16:42Z" },
                ["roleIds"] = new AttributeValue 
                {
                    L =
                    {
                        new AttributeValue { S = "b2a3fbf8-fa90-404b-aaca-a1a604bbce6f" },
                        new AttributeValue { S = "c615432a-6b3a-4570-aa3b-18ef77278024" }
                    }
                }
            });

        var item = 
            new TestModels.UserPost 
            { 
                UserId = new Guid("a684e596-2f85-4259-8108-55eff1e4acce"),
                Timestamp = new DateTime(2022, 10, 19, 16, 42, 0, DateTimeKind.Utc)
            };

        // Act
        await dynamoDBClient.DeleteAsync(
            item,
            condition: () => Contains(item.RoleIds, new Guid("c615432a-6b3a-4570-aa3b-18ef77278024")));

        // Assert
        Assert.Empty(
            (await dynamoDB.GetItemAsync(
                tableName, 
                new Dictionary<string, AttributeValue> 
                {
                    ["userId"] = new AttributeValue { S = "a684e596-2f85-4259-8108-55eff1e4acce" },
                    ["timestamp"] = new AttributeValue { S = "2022-10-19T16:42Z" },    
                })).Item);
    }

    [Fact]
    public async Task DeleteAsyncIsNoOpForNonExistingKey()
    {
        // Act
        await dynamoDBClient.DeleteAsync<TestModels.UserPost>(
            (new Guid("4c3af95c-1a5c-460f-ab3b-a2869f6ef3c6"), new DateTime(2022, 10, 18, 16, 42, 0, DateTimeKind.Utc)));
    }       
}

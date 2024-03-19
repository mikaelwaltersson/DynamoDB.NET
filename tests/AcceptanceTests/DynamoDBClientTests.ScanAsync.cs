using Amazon.DynamoDBv2.Model;

namespace DynamoDB.Net.Tests.AcceptanceTests;

public partial class DynamoDBClientTests
{
    [Fact]
    public async Task ScanAsyncReturnsItems()
    {
        // Arrange
        for (var i = 0; i < 3; i++)
        {
            await dynamoDB.PutItemAsync(
                tableName, 
                new Dictionary<string, AttributeValue>
                {
                    ["userId"] = new AttributeValue { S = $"51b78930-5854-4e91-b4c2-{1 + i:D12}" },
                    ["timestamp"] = new AttributeValue { S = $"2022-10-18T16:{1 + i:D2}:00.0000000+00:00" },
                    ["roleIds"] = new AttributeValue 
                    {
                        L =
                        {
                            new AttributeValue { S = $"0ae4be37-7156-48a6-8598-{1 + i:D12}" },
                            new AttributeValue { S = $"9f825216-3ed1-4543-89d6-{1 + i:D12}" }
                        }
                    }
                });
        }

        // Act
        var items = await dynamoDBClient.ScanAsync<TestModels.UserPost>();

        // Assert
        Assert.Equal(3, items.Count);
        Snapshot.Match(items);
    }
}

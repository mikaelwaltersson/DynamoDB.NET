using Amazon.DynamoDBv2.Model;
using DynamoDB.Net.Tests.FunctionalTests.Common;
using DynamoDB.Net.Tests.FunctionalTests.Common.Models;

namespace DynamoDB.Net.Tests.FunctionalTests.TestModels;

public class DeleteTests : FunctionalTestSuiteBase
{
    [SeedItem]
    readonly User existingUser1 = 
        new()
        { 
            UserId = new Guid("8670ca59-b316-43b2-a6bf-103825793018"), 
            Username = "johndoe", FullName = "John Doe" 
        };

    [SeedItem]
    readonly UserNotification existingUserNotification1 =
        new()
        { 
            UserId = new Guid("8670ca59-b316-43b2-a6bf-103825793018"), 
            Timestamp = DateTimeOffset.Parse("2024-04-15T08:41:00Z"),
            Message = "Your account will soon be available." 
        };

    [Fact]
    public async Task CanDeleteItemsAsync()
    {
        var user = existingUser1;
        var userNotification = existingUserNotification1;

        await DynamoDBClient.DeleteAsync(PrimaryKey.ForItem(user));
        await DynamoDBClient.DeleteAsync(PrimaryKey.ForItem(userNotification)); 
        
        Assert.Empty(
            from entry in await GetAllStoredRawItemsAsync() 
            from item in entry.Items
            select item);
    }

    [Fact]
    public async Task DeleteItemDoesNothingIfKeyDoesNotMatchAsync()
    {
        var user = existingUser1 with { UserId = Guid.NewGuid() };
        var userNotification = existingUserNotification1 with { UserId = user.UserId };

        await DynamoDBClient.DeleteAsync(PrimaryKey.ForItem(user));
        await DynamoDBClient.DeleteAsync(PrimaryKey.ForItem(userNotification)); 
                
        Assert.NotEmpty(
            from entry in await GetAllStoredRawItemsAsync() 
            from item in entry.Items
            select item);
    }

    [Fact]
    public async Task DeleteItemFailsIfConditionIsNotSatisfiedAsync()
    {
        var user = existingUser1;

        await Assert.ThrowsAsync<ConditionalCheckFailedException>(() => 
            DynamoDBClient.DeleteAsync(user, condition: () => user.FullName!.StartsWith("Jane") ));
    }
}

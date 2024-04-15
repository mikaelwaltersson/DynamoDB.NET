using DynamoDB.Net.Tests.FunctionalTests.Common;
using DynamoDB.Net.Tests.FunctionalTests.Common.Models;

namespace DynamoDB.Net.Tests.FunctionalTests.TestModels;

public class GetTests : FunctionalTestSuiteBase
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
    public async Task CanRetrieveItemsAsync()
    {
        var user = await DynamoDBClient.GetAsync(PrimaryKey.ForItem(existingUser1));
        var userNotification = await DynamoDBClient.GetAsync(PrimaryKey.ForItem(existingUserNotification1)); 
        
        Snapshot.Match(new { user, userNotification });
    }

    [Fact]
    public async Task RetrieveItemFailsIfKeyDoesNotMatchAsync()
    {
        var user = existingUser1 with { UserId = Guid.NewGuid() };
        var userNotification = existingUserNotification1 with { UserId = user.UserId };

        await Assert.ThrowsAsync<ItemNotFoundException<User>>(() => 
            DynamoDBClient.GetAsync(PrimaryKey.ForItem(user)));

        await Assert.ThrowsAsync<ItemNotFoundException<UserNotification>>(() => 
            DynamoDBClient.GetAsync(PrimaryKey.ForItem(userNotification)));
    }
}

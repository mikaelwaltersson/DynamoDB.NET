using DynamoDB.Net.Tests.FunctionalTests.Common;
using DynamoDB.Net.Tests.FunctionalTests.Common.Models;

namespace DynamoDB.Net.Tests.FunctionalTests.TestModels;

public class TryGetTests : FunctionalTestSuiteBase
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
        var user = await DynamoDBClient.TryGetAsync(PrimaryKey.ForItem(existingUser1));
        var userNotification = await DynamoDBClient.TryGetAsync(PrimaryKey.ForItem(existingUserNotification1)); 
        
        Assert.NotNull(user);
        Assert.NotNull(userNotification);
        Snapshot.Match(new { user, userNotification });
    }

    [Fact]
    public async Task RetrieveItemReturnsNullIfKeyDoesNotMatchAsync()
    {
        var user = existingUser1 with { UserId = Guid.NewGuid() };
        var userNotification = existingUserNotification1 with { UserId = user.UserId };

       Assert.Null(await DynamoDBClient.TryGetAsync(PrimaryKey.ForItem(user)));
       Assert.Null(await DynamoDBClient.TryGetAsync(PrimaryKey.ForItem(userNotification)));
    }
}

using Amazon.DynamoDBv2.Model;
using DynamoDB.Net.Tests.FunctionalTests.Common;
using DynamoDB.Net.Tests.FunctionalTests.Common.Models;
using static DynamoDB.Net.DynamoDBExpressions;

namespace DynamoDB.Net.Tests.FunctionalTests.TestModels;

public class UpdateTests : FunctionalTestSuiteBase
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
    public async Task CanUpdateItemsAsync()
    {
        var user = existingUser1;
        var userNotification = existingUserNotification1;

        user = await DynamoDBClient.UpdateAsync(user, () => Remove(user.FullName));
        userNotification = await DynamoDBClient.UpdateAsync(userNotification, () => Set(userNotification.Message, "<REDACTED>"));
        
        var storedItems = (await GetAllStoredRawItemsAsync()).ToSnapshotFriendlyObject();

        Snapshot.Match(
            new 
            {
                user,
                userNotification,
                storedItems
            });
    }

    [Fact]
    public async Task UpdateFailsIfVersionDoesNotMatchAsync()
    {
        var user = existingUser1;

        await Assert.ThrowsAsync<ConditionalCheckFailedException>(() => 
            DynamoDBClient.UpdateAsync(
                user, 
                () => Remove(user.FullName),
                version: 2));
    }

    [Fact]
    public async Task UpdateItemFailsIfConditionIsNotSatisfiedAsync()
    {
        var user = existingUser1;
        var userNotification = existingUserNotification1;

        await Assert.ThrowsAsync<ConditionalCheckFailedException>(() => 
            DynamoDBClient.UpdateAsync(user, 
            () => Remove(user.FullName), 
            condition: () => user.FullName == null));

        await Assert.ThrowsAsync<ConditionalCheckFailedException>(() =>
            DynamoDBClient.UpdateAsync(
                userNotification, 
                () => Set(userNotification.Message, "<REDACTED>"), 
                condition: () => userNotification.Message.Count() > 400));
    }
}

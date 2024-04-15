using Amazon.DynamoDBv2.Model;
using DynamoDB.Net.Tests.FunctionalTests.Common;
using DynamoDB.Net.Tests.FunctionalTests.Common.Models;

using static DynamoDB.Net.DynamoDBExpressions;

namespace DynamoDB.Net.Tests.FunctionalTests;

public class PutTests : FunctionalTestSuiteBase
{
    [Fact]
    public async Task CanStoreItemsAsync()
    {
        var user = 
            await DynamoDBClient.PutAsync(
                new User 
                { 
                    UserId = new("4f7e4ea9-c642-43da-9c8b-169dc6a03824"), 
                    Username = "johndoe", FullName = "John Doe" 
                });
        
        var account1 = 
            await DynamoDBClient.PutAsync(
                new Account 
                { 
                    UserId = user.UserId, AccountName = "Savings Account", 
                    AccountNumber = "123001-13330001", DateOpened = new(2024, 01, 20), 
                    Balance = 2500.0m 
                });
        
        var account2 = 
            await DynamoDBClient.PutAsync(
                new Account 
                { 
                    UserId = user.UserId, AccountName = "Transaction Account", 
                    AccountNumber = "123001-13330002", DateOpened = new(2024, 01, 27), 
                    Balance = 450.54m 
                });
        
        var transaction1 = 
            await DynamoDBClient.PutAsync(
                new Transaction 
                { 
                    TransactionId = new("70c0712e-276e-43d1-9dc4-db776cf24590"), 
                    ToAccountNumber = account1.AccountNumber, 
                    TransactionDate = account1.DateOpened, TimeOfDay = new(15, 23, 01), 
                    Amount = 3000, TransactionType = TransactionType.Deposit 
                });
        
        var transaction2 =
             await DynamoDBClient.PutAsync(
                new Transaction 
                { 
                    TransactionId = new("ab92a927-8c47-4d9d-addc-69ec4aaa6754"),
                    FromAccountNumber = account1.AccountNumber, ToAccountNumber = account2.AccountNumber, 
                    TransactionDate = account2.DateOpened, TimeOfDay = new(10, 15, 35), 
                    Amount = 500, TransactionType = TransactionType.Transfer 
                });
        
        var transaction3 = 
            await DynamoDBClient.PutAsync(
                new Transaction 
                { 
                    TransactionId = new("b3340941-f00e-4cc8-9d5f-8d07b3075f16"), 
                    FromAccountNumber = account2.AccountNumber, 
                    TransactionDate = account2.DateOpened, TimeOfDay = new(18, 55, 08), 
                    Amount = 49.46m, TransactionType = TransactionType.Purchase 
                });

        var userNotification =
            await DynamoDBClient.PutAsync(
                new UserNotification()
                { 
                    UserId = user.UserId, 
                    Timestamp = new DateTimeOffset(transaction2.TransactionDate, transaction2.TimeOfDay, default),
                    Message = "Your accounts will soon be available." 
                });

        var storedItems = (await GetAllStoredRawItemsAsync()).ToSnapshotFriendlyObject();

        Snapshot.Match(
            new 
            {
                user,
                account1,
                account2,
                transaction1,
                transaction2,
                transaction3,
                userNotification,
                storedItems
            });
    }

    [Fact]
    public async Task StoreItemFailsIfConditionIsNotSatisfiedAsync()
    {
        var user = 
            new User 
            { 
                UserId = new("1992dad8-7e14-4276-a697-327d68959dbf"), 
                Username = "johndoe", FullName = "John Doe" 
            };
        
        await Assert.ThrowsAsync<ConditionalCheckFailedException>(() => 
            DynamoDBClient.PutAsync(user, condition: () => AttributeExists(user.UserId)));
    }
}

using DynamoDB.Net.Tests.FunctionalTests.Common;
using DynamoDB.Net.Tests.FunctionalTests.Common.Models;
using static DynamoDB.Net.DynamoDBExpressions;

namespace DynamoDB.Net.Tests.FunctionalTests.TestModels;

public class WriteTransactionTests : FunctionalTestSuiteBase
{
    [SeedItem]
    readonly User user = 
        new()
        { 
            UserId = new("4f7e4ea9-c642-43da-9c8b-169dc6a03824"), 
            Username = "johndoe", FullName = "John Doe",
            UserStatus = UserStatus.Active
        };
    
    [SeedItem]
    readonly Account account1 = 
        new()
        { 
            UserId = new("4f7e4ea9-c642-43da-9c8b-169dc6a03824"), 
            AccountName = "Savings Account", 
            AccountNumber = "123001-13330001", DateOpened = new(2024, 01, 20), 
            Balance = 2600.0m,
            AccountStatus = AccountStatus.Active
        };
    
    [SeedItem]
    readonly Account account2 = 
        new() 
        { 
            UserId = new("4f7e4ea9-c642-43da-9c8b-169dc6a03824"), 
            AccountName = "Transaction Account", 
            AccountNumber = "123001-13330002", DateOpened = new(2024, 01, 20), 
            Balance = 400.0m,
            AccountStatus = AccountStatus.Active
        };

    [SeedItem]
    readonly Account account3 = 
        new() 
        { 
            UserId = new("4f7e4ea9-c642-43da-9c8b-169dc6a03824"), 
            AccountName = "Transaction Account", 
            AccountNumber = "123001-13330003", DateOpened = new(2024, 01, 27), 
            Balance = 0.0m,
            AccountStatus = AccountStatus.Active
        };

    [SeedItem]
    readonly Transaction transaction1 = 
        new()
        { 
            TransactionId = new("70c0712e-276e-43d1-9dc4-db776cf24590"), 
            FromAccountNumber = "123001-13330001", ToAccountNumber = "123001-13330002",
            TransactionDate = new(2024, 01, 20), TimeOfDay = new(15, 23, 01), 
            Amount = 400m, TransactionType = TransactionType.Transfer 
        };
    

    [Fact]
    public async Task CanApplyMultipleOperationsInSingleWriteTransaction()
    {
        var newTransaction = 
            new Transaction()
            {
                TransactionId = new("6f708458-1e2a-4393-b69c-22b03455f5b9"), 
                FromAccountNumber = "123001-13330001", ToAccountNumber = "123001-13330003",
                TransactionDate = new(2024, 01, 27), TimeOfDay = new(10, 27, 26), 
                Amount = 300, TransactionType = TransactionType.Transfer 
            };

        await DynamoDBClient
            .BeginWriteTransaction()
            .ConditionCheck(
                user, 
                condition: () => user.UserStatus != UserStatus.Deleted)
            .Update(
                account1, 
                update: () => Set(account1.Balance, 2700m), 
                condition: () => account1.Balance == 2600m && account1.AccountStatus == AccountStatus.Active)
            .Update(
                account2, 
                update: () => 
                    Set(account2.Balance, 0m) & 
                    Set(account2.AccountStatus, AccountStatus.Terminated) &
                    Set(account2.Notes, 
                        ListAppend(
                            IfNotExists(account2.Notes, new()),
                            new[] { "Account has been terminated and transactions" })),
                condition: () => account2.Balance == 400m && account2.AccountStatus == AccountStatus.Active)
            .Update(
                account3, 
                update: () => Set(account3.Balance, 300m),
                condition: () => account3.Balance == 0m && account3.AccountStatus == AccountStatus.Active)
            .Delete(
                transaction1,
                condition: () => AttributeExists(transaction1.TransactionId))
            .Put(
                newTransaction,
                condition: () => AttributeNotExists(newTransaction.TransactionId))
            .CommitAsync();

        var storedItems = (await GetAllStoredRawItemsAsync()).ToSnapshotFriendlyObject();

        Snapshot.Match(new { storedItems });
    }    
}

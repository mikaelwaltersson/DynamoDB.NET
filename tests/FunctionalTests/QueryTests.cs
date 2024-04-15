using DynamoDB.Net.Tests.FunctionalTests.Common;
using DynamoDB.Net.Tests.FunctionalTests.Common.Models;

namespace DynamoDB.Net.Tests.FunctionalTests.TestModels;

public class QueryTests : FunctionalTestSuiteBase
{
    [SeedItem]
    readonly User user1 = 
        new()
        { 
            UserId = new("4f7e4ea9-c642-43da-9c8b-169dc6a03824"), 
            Username = "johndoe", FullName = "John Doe" 
        };
    
    [SeedItem]
    readonly User user2 = 
        new()
        { 
            UserId = new("9e66d05f-8a82-478b-a443-cf668678c3ae"), 
            Username = "adalovelace", FullName = "Ada Lovelace" 
        };
    

    [SeedItem]
    readonly Account account1 = 
        new()
        { 
            UserId = new("4f7e4ea9-c642-43da-9c8b-169dc6a03824"), 
            AccountName = "Savings Account", 
            AccountNumber = "123001-13330001", DateOpened = new(2024, 01, 20), 
            Balance = 2500.0m 
        };
    
    [SeedItem]
    readonly Account account2 = 
        new() 
        { 
            UserId = new("4f7e4ea9-c642-43da-9c8b-169dc6a03824"), 
            AccountName = "Transaction Account", 
            AccountNumber = "123001-13330002", DateOpened = new(2024, 01, 27), 
            Balance = 450.54m 
        };
    
    [SeedItem]
    readonly Transaction transaction1 = 
        new()
        { 
            TransactionId = new("70c0712e-276e-43d1-9dc4-db776cf24590"), 
            ToAccountNumber = "123001-13330001", 
            TransactionDate = new(2024, 01, 20), TimeOfDay = new(15, 23, 01), 
            Amount = 3000, TransactionType = TransactionType.Deposit 
        };
    
    [SeedItem]
    readonly Transaction transaction2 =
        new() 
        { 
            TransactionId = new("ab92a927-8c47-4d9d-addc-69ec4aaa6754"),
            FromAccountNumber = "123001-13330001", ToAccountNumber = "123001-13330002", 
            TransactionDate = new(2024, 01, 27), TimeOfDay = new(10, 15, 35), 
            Amount = 500, TransactionType = TransactionType.Transfer 
        };
    
    [SeedItem]
    readonly Transaction transaction3 =
        new() 
        { 
            TransactionId = new("b3340941-f00e-4cc8-9d5f-8d07b3075f16"), 
            FromAccountNumber = "123001-13330002", 
            TransactionDate = new(2024, 01, 27), TimeOfDay = new(18, 55, 08), 
            Amount = 49.46m, TransactionType = TransactionType.Purchase 
        };

    [SeedItem]
    readonly UserNotification userNotification1 =
        new()
        { 
            UserId = new("4f7e4ea9-c642-43da-9c8b-169dc6a03824"), 
            Timestamp = new DateTimeOffset(new(2024, 01, 27), new(10, 15, 35), default),
            Message = "Your accounts will soon be available." 
        };

    [SeedItem]
    readonly UserNotification userNotification2 =
        new()
        { 
            UserId = new("4f7e4ea9-c642-43da-9c8b-169dc6a03824"), 
            Timestamp = new DateTimeOffset(new(2024, 01, 27), new(18, 55, 09), default),
            Message = "A purchase was made." 
        };


    [SeedItem]
    readonly UserNotification userNotification3 =
        new()
        { 
            UserId = new("9e66d05f-8a82-478b-a443-cf668678c3ae"), 
            Timestamp = new DateTimeOffset(new(2024, 01, 28), new(14, 35, 52), default),
            Message = "Your accounts will soon be available." 
        };

    [Fact]
    public async void CanRetrieveItemsByPartitionKey()
    {
        Assert.Equal(
            [userNotification1, userNotification2], 
            await DynamoDBClient.QueryRemainingAsync<UserNotification>(
                transaction => transaction.UserId == user1.UserId));

        Assert.Equal(
            [userNotification3], 
            await DynamoDBClient.QueryRemainingAsync<UserNotification>(
                transaction => transaction.UserId == user2.UserId));
    }

    [Fact]
    public async void CanRetrieveItemsBySecondaryIndex()
    {
        Assert.Equal(
            [account1, account2], 
            await DynamoDBClient.QueryRemainingAsync<Account>(
                account => account.UserId == user1.UserId));

        Assert.Equal(
            [], 
            await DynamoDBClient.QueryRemainingAsync<Account>(
                account => account.UserId == user2.UserId));

        Assert.Equal(
            [transaction1], 
            await DynamoDBClient.QueryAsync<Transaction>(
                transaction => 
                    transaction.ToAccountNumber == account1.AccountNumber &&
                    transaction.TransactionDate >= new DateOnly(2024, 01, 01)));

        Assert.Equal(
            [transaction2], 
            await DynamoDBClient.QueryAsync<Transaction>(
                transaction => transaction.ToAccountNumber == account2.AccountNumber));

        Assert.Equal(
            [transaction3], 
            await DynamoDBClient.QueryAsync<Transaction>(
                transaction => transaction.FromAccountNumber == account2.AccountNumber,
                filter: transaction => transaction.TransactionType == TransactionType.Purchase));

    }
}

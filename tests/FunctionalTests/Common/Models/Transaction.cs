using DynamoDB.Net.Model;

namespace DynamoDB.Net.Tests.FunctionalTests.Common.Models;

[Table]
public record Transaction
{
    [PartitionKey]
    public required Guid TransactionId { get; set; }

    [PartitionKey(GlobalSecondaryIndex = 0)]
    public string? FromAccountNumber { get; set; } 

    [PartitionKey(GlobalSecondaryIndex = 1)]
    public string? ToAccountNumber { get; set; } 

    [SortKey(GlobalSecondaryIndex = 0), SortKey(GlobalSecondaryIndex = 1)]
    public required DateOnly TransactionDate { get; set; }

    public required TimeOnly TimeOfDay { get; set; }

    public decimal Amount { get; set; }

    public TransactionType TransactionType { get; set; }

    [Version, DynamoDBProperty(AttributeName = "$version")]
    public long ItemVersion { get; set; }
}

using DynamoDB.Net.Model;

namespace DynamoDB.Net.Tests.FunctionalTests.Common.Models;

[Table]
public record Account
{
    [PartitionKey, SortKey(GlobalSecondaryIndex = 0)]
    public required string AccountNumber { get; set; } 

    public AccountStatus AccountStatus { get; set; } 

    [PartitionKey(GlobalSecondaryIndex = 0)]
    public required Guid UserId { get; set; }

    public required string AccountName { get; set; }

    public required DateOnly DateOpened { get; set; }

    public decimal? Balance { get; set; }

    [Version, DynamoDBProperty(AttributeName = "$version")]
    public long ItemVersion { get; set; }

    public List<string>? Notes { get; set; }
}

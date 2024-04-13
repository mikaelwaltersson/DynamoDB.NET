using DynamoDB.Net.Model;

namespace DynamoDB.Net.Tests.FunctionalTests.TestModels;

[Table(TableName = "user-posts")]
public class UserPost
{
    [PartitionKey]
    public required Guid UserId { get; set; }

    [SortKey]
    public required DateTimeOffset Timestamp { get; set; }

    public List<Guid> LinkedPostIds { get; set; } = [];

    public string? Content { get; set; }

    [SortKey(LocalSecondaryIndex = 0)]
    public int Priority { get; set; }

    public SortedSet<string> Tags { get; set; } = [];

    [PartitionKey(GlobalSecondaryIndex = 0)]
    public string? ExternalId { get; set; }

    [Version]
    public long Version { get; set; }
}

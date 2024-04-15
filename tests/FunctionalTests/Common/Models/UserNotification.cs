using DynamoDB.Net.Model;

namespace DynamoDB.Net.Tests.FunctionalTests.Common.Models;

[Table]
public record UserNotification
{
    [PartitionKey]
    public required Guid UserId { get; set; }

    [SortKey]
    public required DateTimeOffset Timestamp { get; set; }

    public required string Message { get; set; }
}

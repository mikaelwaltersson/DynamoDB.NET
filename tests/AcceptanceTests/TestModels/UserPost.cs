namespace DynamoDB.Net.Tests.AcceptanceTests.TestModels;

using DynamoDB.Net.Model;

[Table]
public class UserPost
{
    [PartitionKey]
    public required Guid UserId { get; set; }

    [SortKey]
    public required DateTime Timestamp { get; set; }

    public List<Guid> RoleIds { get; set; } = new List<Guid>();
}

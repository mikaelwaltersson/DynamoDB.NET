using DynamoDB.Net.Model;

namespace DynamoDB.Net.Tests.FunctionalTests.Common.Models;

[Table]
public record User
{
    [PartitionKey]
    public required Guid UserId { get; set; }

    public UserStatus UserStatus { get; set; }

    public required string Username { get; set; }

    public string? FullName { get; set; }

    [Version, DynamoDBProperty(AttributeName = "$version")]
    public long ItemVersion { get; set; }
}

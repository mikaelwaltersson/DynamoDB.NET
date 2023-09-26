using DynamoDB.Net.Model;

namespace DynamoDB.NET.Tests.TestModels
{
    [Table]
    public class UserPost
    {
        [PartitionKey]
        public Guid UserId { get; set; }

        [SortKey]
        public DateTime Timestamp { get; set; }

        public List<Guid> RoleIds { get; set; } = new List<Guid>();
    }
}
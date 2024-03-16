using System.Collections.Generic;
using DynamoDB.Net.Serialization;

namespace DynamoDB.Net
{
    public class DynamoDBClientOptions
    {
        public string TableNamePrefix { get; set; } = string.Empty;
        
        public IDictionary<string, string> TableNameMappings { get; set; } = new Dictionary<string, string>();

        public bool DefaultConsistentRead { get; set; } = false;
        
        public SerializeDynamoDBValueFlags SerializeFlags { get; set; }
    }
}
namespace DynamoDB.Net;

public class DynamoDBClientOptions
{
    public string TableNamePrefix { get; set; } = string.Empty;
    
    public Dictionary<string, string> TableNameMappings { get; set; } = [];

    public bool DefaultConsistentRead { get; set; } = false;
}

namespace DynamoDB.Net.Serialization;

static class MemoryStreamExtensions
{
    public static string ToBase64String(this MemoryStream value) => 
        Convert.ToBase64String(value.ToArray());
}

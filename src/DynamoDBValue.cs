namespace DynamoDB.Net;

public static class DynamoDBValue
{
    public static bool IsEmptyOrNull(object? value) => 
        value == null ||
        (value is string stringValue && stringValue.Length == 0) ||
        (value is System.Collections.ICollection collectionValue && collectionValue.Count == 0);
}

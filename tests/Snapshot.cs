using Amazon.DynamoDBv2.Model;
using DynamoDB.Net.Serialization;

namespace DynamoDB.Net.Tests;

public static class Snapshot
{
    public static void Match(object value) =>
        Snapshooter.Xunit.Snapshot.Match(value.ToSnapshotFriendlyObject());

     static object? ToSnapshotFriendlyObject<T>(this T value)
     {
        if (value is List<Dictionary<string, AttributeValue>> dynamoDBItemList)
            return dynamoDBItemList.Select(ToSnapshotFriendlyObject).ToList();
        
        if (value is Dictionary<string, AttributeValue> dynamoDBItem)
            return dynamoDBItem.ToDictionary(entry => entry.Key, entry => entry.Value.ToSnapshotFriendlyObject());

        if (value is AttributeValue dynamoDBValue)
        {
            if (dynamoDBValue.NULL)
                return new { NULL = true };

            if (dynamoDBValue.IsBOOLSet)
                return new { dynamoDBValue.BOOL };

            if (dynamoDBValue.S is not null)
                return new { dynamoDBValue.S };

            if (dynamoDBValue.N is not null)
                return new { dynamoDBValue.N };

            if (dynamoDBValue.B is not null)
                return new { B = dynamoDBValue.B.ToBase64String() };

            if (dynamoDBValue.SS is { Count: > 0 })
                return  new { dynamoDBValue.SS };
            
            if (dynamoDBValue.NS is { Count: > 0 })
                return  new { dynamoDBValue.NS };
        
            if (dynamoDBValue.BS is { Count: > 0 })
                return  new { BS = dynamoDBValue.BS.Select(b => b.ToBase64String()).ToList() };

            if (dynamoDBValue.IsLSet)
                return  new { L = dynamoDBValue.L.Select(ToSnapshotFriendlyObject).ToList() };

            if (dynamoDBValue.IsMSet)
                return  new { M = dynamoDBValue.M.ToSnapshotFriendlyObject() };
        }

        return value;
     }
}

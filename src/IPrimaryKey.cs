using DynamoDB.Net.Serialization;

namespace DynamoDB.Net;

public interface IPrimaryKey
{
    object PartitionKey { get; }

    object SortKey { get; }

    string ToString(IDynamoDBSerializer serializer = null, char separator = ',');
}
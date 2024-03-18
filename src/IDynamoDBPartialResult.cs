using System.Collections.Generic;

namespace DynamoDB.Net;

public interface IDynamoDBPartialResult : IDynamoDBPartialResult<object, IPrimaryKey>
{
}

public interface IDynamoDBPartialResult<T> : IDynamoDBPartialResult<T, PrimaryKey<T>> where T : class
{
}

public interface IDynamoDBPartialResult<T, out TKey> : IReadOnlyList<T> where T : class
{
    TKey LastEvaluatedKey { get; }
}

using System.Collections;
using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace DynamoDB.Net;

public static class DynamoDBClientExtensions
{
    public static Task<object> GetAsync(
        this IDynamoDBClient client,
        IPrimaryKey key,
        bool? consistentRead = false,
        CancellationToken cancellationToken = default) =>
        ItemOperationsForKeyType(key).GetAsync(client, key, consistentRead, cancellationToken);

    public static Task<object?> TryGetAsync(
        this IDynamoDBClient client,
        IPrimaryKey key,
        bool? consistentRead = false,
        CancellationToken cancellationToken = default) =>
        ItemOperationsForKeyType(key).TryGetAsync(client, key, consistentRead, cancellationToken);

    public static Task<object> PutAsync(
        this IDynamoDBClient client,
        object item,
        CancellationToken cancellationToken = default) =>
        ItemOperationsForEntityType(item.GetType()).PutAsync(client, item, cancellationToken);


    public static Task<IDynamoDBPartialResult> ScanAsync(
        this IDynamoDBClient client,
        Type entityType,
        IPrimaryKey? exclusiveStartKey = null, 
        int? limit = null,
        bool consistentRead = false,
        CancellationToken cancellationToken = default) =>
        ItemOperationsForEntityType(entityType).ScanAsync(client, exclusiveStartKey, limit, consistentRead, cancellationToken);

    public static Task<IReadOnlyList<T>> ScanRemainingAsync<T>(
        this IDynamoDBClient client,
        Expression<Func<T, bool>>? filter = null, 
        PrimaryKey<T> exclusiveStartKey = default, 
        int? limit = null,
        bool consistentRead = false,
        (string?, string?) indexProperties = default,
        CancellationToken cancellationToken = default) where T : class =>
        RemainingAsync(
            new List<T>(),
            exclusiveStartKey, 
            limit,
            (lastEvaluatedKey, remainingLimit) => client.ScanAsync(filter, lastEvaluatedKey, remainingLimit, consistentRead, indexProperties, cancellationToken));

    public static Task<IReadOnlyList<object>> ScanRemainingAsync(
        this IDynamoDBClient client,
        Type entityType,
        IPrimaryKey? exclusiveStartKey = null, 
        int? limit = null,
        bool consistentRead = false,
        CancellationToken cancellationToken = default) =>
        RemainingAsync(
            new List<object>(),
            exclusiveStartKey, 
            limit,
            (lastEvaluatedKey, remainingLimit) => client.ScanAsync(entityType, lastEvaluatedKey, remainingLimit, consistentRead, cancellationToken));

    public static Task<IReadOnlyList<T>> QueryRemainingAsync<T>(
        this IDynamoDBClient client,
        Expression<Func<T, bool>> keyCondition,
        Expression<Func<T, bool>>? filter = null, 
        PrimaryKey<T> exclusiveStartKey = default, 
        bool? scanIndexForward = null,
        int? limit = null,
        bool? consistentRead = false,
        (string?, string?) indexProperties = default,
        CancellationToken cancellationToken = default) where T : class =>
        RemainingAsync(
            new List<T>(),
            exclusiveStartKey, 
            limit,
            (lastEvaluatedKey, remainingLimit) => client.QueryAsync(keyCondition, filter, lastEvaluatedKey, scanIndexForward, remainingLimit, consistentRead, indexProperties, cancellationToken));


    async static Task<IReadOnlyList<T>> RemainingAsync<T, TKey, TPartialResult>(List<T> result, TKey? exclusiveStartKey, int? limit, Func<TKey?, int?, Task<TPartialResult>> next) 
        where T : class
        where TKey : IPrimaryKey
        where TPartialResult : IDynamoDBPartialResult<T, TKey>
    {
        var lastEvaluatedKey = exclusiveStartKey;

        do
        {
            var partialResult = await next(lastEvaluatedKey, limit);

            result.AddRange(partialResult);
            lastEvaluatedKey = partialResult.LastEvaluatedKey;

            if (limit.HasValue)
            {
                limit -= partialResult.Count;
                if (!(limit > 0))
                    break;
            }
        }
        while (lastEvaluatedKey.PartitionKey != null);

        return result;
    }



    static readonly ConcurrentDictionary<Type, IItemOperations> itemOperations = [];

    static IItemOperations ItemOperationsForKeyType(IPrimaryKey key) =>
        itemOperations.GetOrAdd(
            key.GetType(), 
            static type =>
            {
                if (!PrimaryKey.IsPrimaryKeyType(type, out var itemType))
                    throw new ArgumentOutOfRangeException(nameof(key));

                return (IItemOperations)Serialization.Activator.CreateInstance(typeof(ItemOperations<>).MakeGenericType(itemType));
            });

    static IItemOperations ItemOperationsForEntityType(Type entityType) =>
        itemOperations.GetOrAdd(
            entityType, 
            type =>
            {
                if (type == null)
                    throw new ArgumentOutOfRangeException(nameof(entityType));

                return (IItemOperations)Serialization.Activator.CreateInstance(typeof(ItemOperations<>).MakeGenericType(type));
            });


    interface IItemOperations
    {
        Task<object> GetAsync(IDynamoDBClient client, IPrimaryKey key, bool? consistentRead, CancellationToken cancellationToken);
       
        Task<object?> TryGetAsync(IDynamoDBClient client, IPrimaryKey key, bool? consistentRead, CancellationToken cancellationToken);
        
        Task<object> PutAsync(IDynamoDBClient client, object item, CancellationToken cancellationToken);
        
        Task<IDynamoDBPartialResult> ScanAsync(IDynamoDBClient client, IPrimaryKey? exclusiveStartKey, int? limit, bool? consistentRead, CancellationToken cancellationToken);
    }

    class ItemOperations<T> : IItemOperations where T : class
    {
        public async Task<object> GetAsync(IDynamoDBClient client, IPrimaryKey key, bool? consistentRead, CancellationToken cancellationToken) =>
            await client.GetAsync((PrimaryKey<T>)key, consistentRead, cancellationToken);

        public async Task<object?> TryGetAsync(IDynamoDBClient client, IPrimaryKey key, bool? consistentRead, CancellationToken cancellationToken) =>
            await client.TryGetAsync((PrimaryKey<T>)key, consistentRead, cancellationToken);

        public async Task<object> PutAsync(IDynamoDBClient client, object item, CancellationToken cancellationToken) =>
            await client.PutAsync((T)item, (Expression<Func<T, bool>>?)null, cancellationToken);

        public async Task<IDynamoDBPartialResult> ScanAsync(IDynamoDBClient client, IPrimaryKey? exclusiveStartKey, int? limit, bool? consistentRead, CancellationToken cancellationToken) =>
            new DynamoDBPartialResult(await client.ScanAsync(null, ((PrimaryKey<T>?)exclusiveStartKey).GetValueOrDefault(), limit, consistentRead, (null, null), cancellationToken));

        class DynamoDBPartialResult : IDynamoDBPartialResult
        {
            IDynamoDBPartialResult<T> result;

            public DynamoDBPartialResult(IDynamoDBPartialResult<T> result)
            {
                this.result = result;
            }

            public object this[int index] => result[index];

            public int Count => result.Count;

            public IPrimaryKey LastEvaluatedKey => result.LastEvaluatedKey;

            public IEnumerator<object> GetEnumerator() => result.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => result.GetEnumerator();
        }
    }
}

using System.Collections;
using System.Linq.Expressions;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using DynamoDB.Net.Serialization;
using DynamoDB.Net.Expressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DynamoDB.Net;

public class DynamoDBClient : IDynamoDBClient
{
    readonly IAmazonDynamoDB client;
    readonly IOptions<DynamoDBClientOptions> options;
    readonly IDynamoDBItemEventHandler itemEvents;
    readonly ILogger<DynamoDBClient> logger;
    readonly IDynamoDBSerializer serializer;

    public DynamoDBClient(
        IAmazonDynamoDB client, 
        IOptions<DynamoDBClientOptions> options,
        IDynamoDBSerializer serializer,
        IEnumerable<IDynamoDBItemEventHandler> itemEventHandlers, 
        ILogger<DynamoDBClient> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(serializer);
        ArgumentNullException.ThrowIfNull(itemEventHandlers);
        ArgumentNullException.ThrowIfNull(logger);

        this.client = client;
        this.options = options;
        this.serializer = serializer;
        this.itemEvents = new CombinedDynamoDBItemEventHandlers([..itemEventHandlers]);
        this.logger = logger;
    }

    DynamoDBClientOptions Options => options.Value;

    bool DefaultConsistentRead => Options?.DefaultConsistentRead ?? false;

    ReturnConsumedCapacity LogConsumedCapacity =>
        logger.IsEnabled(LogLevel.Debug)
            ? ReturnConsumedCapacity.INDEXES
            : ReturnConsumedCapacity.NONE;
    

    public Task<T?> TryGetAsync<T>(
        PrimaryKey<T> key,
        bool? consistentRead = false,
        CancellationToken cancellationToken = default) where T : class =>
        GetAsync(key, consistentRead, cancellationToken, throwErrorIfNotExists: false);

    public Task<T> GetAsync<T>(
        PrimaryKey<T> key,
        bool? consistentRead = false,
        CancellationToken cancellationToken = default) where T : class =>
        GetAsync(key, consistentRead, cancellationToken, throwErrorIfNotExists: true)!;

    async Task<T?> GetAsync<T>(
        PrimaryKey<T> key,
        bool? consistendRead,
        CancellationToken cancellationToken,
        bool throwErrorIfNotExists) where T : class
    {
        ArgumentOutOfRangeException.ThrowIfEqual(key, default);

        var request =
            new GetItemRequest
            {
                TableName = TableDescription.GetTableName<T>(Options),
                Key = Serialize(key),
                ConsistentRead = consistendRead ?? DefaultConsistentRead,
                ReturnConsumedCapacity = LogConsumedCapacity
            };

        var response = await Invoke(client.GetItemAsync, request, cancellationToken);

        if (response.IsItemSet)
            return DeserializeItem<T>(response.Item);

        if (throwErrorIfNotExists)
            throw new ItemNotFoundException<T>(key, request.TableName);

        return null;
    }


    public async Task<T> PutAsync<T>(
        T item,
        Expression<Func<T, bool>>? condition = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var operation = CreatePutOperation(item, condition);

        var request =
            new PutItemRequest
            {
                TableName = TableDescription.GetTableName<T>(Options),
                Item = operation.Item,
                ConditionExpression = operation.ConditionExpression,
                ExpressionAttributeNames = operation.ExpressionAttributeNames,
                ExpressionAttributeValues = operation.ExpressionAttributeValues,
                ReturnConsumedCapacity = LogConsumedCapacity,
                ReturnValues = ReturnValue.NONE
            };

        await Invoke(client.PutItemAsync, request, cancellationToken);

        return DeserializeItem<T>(request.Item);
    }

    public async Task<T> UpdateAsync<T>(
        PrimaryKey<T> key,
        Expression<Func<T, DynamoDBExpressions.UpdateAction>> update,
        Expression<Func<T, bool>>? condition = null,
        object? version = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var operation = CreateUpdateOperation(key, update, condition, version);

        var request =
            new UpdateItemRequest
            {                    
                TableName = TableDescription.GetTableName<T>(Options),
                Key = Serialize(key),
                UpdateExpression = operation.UpdateExpression,
                ConditionExpression = operation.ConditionExpression,
                ExpressionAttributeNames = operation.ExpressionAttributeNames,
                ExpressionAttributeValues = operation.ExpressionAttributeValues,
                ReturnConsumedCapacity = LogConsumedCapacity,
                ReturnValues = ReturnValue.ALL_NEW
            };

            var response = await Invoke(client.UpdateItemAsync, request, cancellationToken);

        return DeserializeItem<T>(response.Attributes);
    }

    public Task DeleteAsync<T>(
        PrimaryKey<T> key,
        Expression<Func<T, bool>>? condition = null,
        object? version = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var operation = CreateDeleteOperation(key, condition, version);

        var request =
            new DeleteItemRequest
            {
                TableName = TableDescription.GetTableName<T>(Options),
                Key = Serialize(key),
                ConditionExpression = operation.ConditionExpression,
                ExpressionAttributeNames = operation.ExpressionAttributeNames,
                ExpressionAttributeValues = operation.ExpressionAttributeValues,
                ReturnConsumedCapacity = LogConsumedCapacity,
                ReturnValues = ReturnValue.NONE
            };

        return Invoke(client.DeleteItemAsync, request, cancellationToken);
    }

    public async Task<IDynamoDBPartialResult<T>> ScanAsync<T>(
        Expression<Func<T, bool>>? filter = null,
        PrimaryKey<T> exclusiveStartKey = default,
        int? limit = null,
        bool? consistendRead = false,
        (string?, string?) indexProperties = default,
        CancellationToken cancellationToken = default) where T : class
    {
        var expressionTranslationContext = new ExpressionTranslationContext(serializer);

        var request =
            new ScanRequest
            {
                TableName = TableDescription.GetTableName<T>(Options),
                ExclusiveStartKey = Serialize(exclusiveStartKey),
                IndexName = indexProperties.GetIndexName<T>(),
                FilterExpression = filter?.Translate(expressionTranslationContext),
                ExpressionAttributeNames = expressionTranslationContext.AttributeNames,
                ExpressionAttributeValues = expressionTranslationContext.AttributeValues,   
                ConsistentRead = consistendRead ?? DefaultConsistentRead,
                ReturnConsumedCapacity = LogConsumedCapacity
            };

        if (limit.HasValue)
            request.Limit = limit.Value;

        var response = await Invoke(client.ScanAsync, request, cancellationToken);

        var items = response.Items.Select(DeserializeItem<T>).ToArray();
        var lastEvaluatedKey = DeserializeKey<T>(response.LastEvaluatedKey);

        return new PartialResult<T>(items, lastEvaluatedKey);
    }

    public async Task<IDynamoDBPartialResult<T>> QueryAsync<T>(
        Expression<Func<T, bool>> keyCondition,
        Expression<Func<T, bool>>? filter = null,
        PrimaryKey<T> exclusiveStartKey = default,
        bool? scanIndexForward = null,
        int? limit = null,
        bool? consistentRead = false,
        (string?, string?) indexProperties = default,
        CancellationToken cancellationToken = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(keyCondition);

        var expressionTranslationContext = new ExpressionTranslationContext(serializer);

        var request =
            new QueryRequest
            {
                TableName = TableDescription.GetTableName<T>(Options),
                ExclusiveStartKey = Serialize(exclusiveStartKey),
                KeyConditionExpression = keyCondition.Translate(expressionTranslationContext),
                IndexName = indexProperties != default 
                    ? indexProperties.GetIndexName<T>() 
                    : keyCondition.GetIndexName(),
                FilterExpression = filter?.Translate(expressionTranslationContext),
                ExpressionAttributeNames = expressionTranslationContext.AttributeNames,
                ExpressionAttributeValues = expressionTranslationContext.AttributeValues,  
                ConsistentRead = consistentRead ?? DefaultConsistentRead,
                ReturnConsumedCapacity = LogConsumedCapacity
            };

        if (scanIndexForward.HasValue)
            request.ScanIndexForward = scanIndexForward.Value;

        if (limit.HasValue)
            request.Limit = limit.Value;

        var response = await Invoke(client.QueryAsync, request, cancellationToken);

        var items = response.Items.Select(DeserializeItem<T>).ToArray();
        var lastEvaluatedKey = DeserializeKey<T>(response.LastEvaluatedKey);

        return new PartialResult<T>(items, lastEvaluatedKey);
    }

    public IDynamoDBWriteTransaction BeginWriteTransaction() => new WriteTransaction(this);

    Put CreatePutOperation<T>(
        T item,
        Expression<Func<T, bool>>? condition = null) where T : class
    {
        ArgumentNullException.ThrowIfNull(item);

        var expressionTranslationContext = new ExpressionTranslationContext(serializer);
        
        var version = TableDescription.PropertyAccessors<T>.GetVersion?.Invoke(item);

        var serializedItem = itemEvents.OnItemSerialized<T>(Serialize(item), expressionTranslationContext);
        var translatedItemCondition = itemEvents.OnItemConditionTranslated<T>(condition?.Translate(expressionTranslationContext), version, expressionTranslationContext);

        return
            new Put
            {
                TableName = TableDescription.GetTableName<T>(Options),
                Item = serializedItem,
                ConditionExpression = translatedItemCondition,
                ExpressionAttributeNames = expressionTranslationContext.AttributeNames,
                ExpressionAttributeValues = expressionTranslationContext.AttributeValues,
            };
    }

    Update CreateUpdateOperation<T>(
        PrimaryKey<T> key,
        Expression<Func<T, DynamoDBExpressions.UpdateAction>> update,
        Expression<Func<T, bool>>? condition = null,
        object? version = null) where T : class
    {

        ArgumentOutOfRangeException.ThrowIfEqual(key, default);
        ArgumentNullException.ThrowIfNull(update);

        var expressionTranslationContext = new ExpressionTranslationContext(serializer);

        var translatedItemUpdate = itemEvents.OnItemUpdateTranslated<T>(update.Translate(expressionTranslationContext), version, expressionTranslationContext);
        var translatedItemCondition = itemEvents.OnItemConditionTranslated<T>(condition?.Translate(expressionTranslationContext), version, expressionTranslationContext);

        return 
            new Update
            {
                TableName = TableDescription.GetTableName<T>(Options),
                Key = Serialize(key),
                UpdateExpression = translatedItemUpdate,
                ConditionExpression = translatedItemCondition,
                ExpressionAttributeNames = expressionTranslationContext.AttributeNames,
                ExpressionAttributeValues = expressionTranslationContext.AttributeValues
            };
    }

    Delete CreateDeleteOperation<T>(
        PrimaryKey<T> key,
        Expression<Func<T, bool>>? condition = null,
        object? version = null) where T : class
    {
        ArgumentOutOfRangeException.ThrowIfEqual(key, default);

        var expressionTranslationContext = new ExpressionTranslationContext(serializer);

        var translatedItemCondition = itemEvents.OnItemConditionTranslated<T>(condition?.Translate(expressionTranslationContext), version, expressionTranslationContext);

        return
            new Delete
            {
                TableName = TableDescription.GetTableName<T>(Options),
                Key = Serialize(key),
                ConditionExpression = translatedItemCondition,
                ExpressionAttributeNames = expressionTranslationContext.AttributeNames,
                ExpressionAttributeValues = expressionTranslationContext.AttributeValues
            };
    }

    ConditionCheck CreateConditionCheckOperation<T>(
        PrimaryKey<T> key, 
        Expression<Func<T, bool>> condition, 
        object? version = null) where T : class
    {
        ArgumentOutOfRangeException.ThrowIfEqual(key, default);

        var expressionTranslationContext = new ExpressionTranslationContext(serializer);

        var translatedItemCondition = itemEvents.OnItemConditionTranslated<T>(condition?.Translate(expressionTranslationContext), version, expressionTranslationContext);

        return 
            new ConditionCheck
            {
                TableName = TableDescription.GetTableName<T>(Options),
                Key = Serialize(key),
                ConditionExpression = translatedItemCondition,
                ExpressionAttributeNames = expressionTranslationContext.AttributeNames,
                ExpressionAttributeValues = expressionTranslationContext.AttributeValues
            };
    }

    Dictionary<string, AttributeValue> Serialize<T>(T itemOrKeys) => 
        serializer.SerializeDynamoDBValue(itemOrKeys).EnsureIsMSet().M;

    T Deserialize<T>(Dictionary<string, AttributeValue> attributes) =>
        serializer.DeserializeDynamoDBValue<T>(new AttributeValue { M = attributes, IsMSet = true })!;

    T DeserializeItem<T>(Dictionary<string, AttributeValue> attributes) where T : class =>
        itemEvents.OnItemDeserialized(Deserialize<T>(attributes));

    PrimaryKey<T> DeserializeKey<T>(Dictionary<string, AttributeValue> attributes) where T : class =>
        attributes.Count == 0 ? default : Deserialize<PrimaryKey<T>>(attributes);

    async Task<TResponse> Invoke<TRequest, TResponse>(
        Func<TRequest, CancellationToken, Task<TResponse>> operation, 
        TRequest request, 
        CancellationToken cancellationToken)
        where TRequest : AmazonDynamoDBRequest
        where TResponse : AmazonWebServiceResponse
    {
        logger.InvokeBegin(request);
        try
        {
            var response = await operation(request, cancellationToken);

            logger.InvokeSuccess(response);

            return response;
        }
        catch (ConditionalCheckFailedException ex)
        {
            logger.ConditionalCheckFailed(ex);    
            throw;
        }
        catch (TransactionCanceledException ex)
        {
            logger.TransactionCanceled(ex);    
            throw;
        }
        catch (AmazonDynamoDBException ex)
        {
            logger.InvokeFailed(ex, ex.ErrorCode);    
            throw;
        }
    } 

    class PartialResult<T> : IDynamoDBPartialResult<T> where T : class
    {
        T[] items;
        PrimaryKey<T> lastEvaluatedKey;

        public PartialResult(T[] items, PrimaryKey<T> lastEvaluatedKey)
        {
            this.items = items;
            this.lastEvaluatedKey = lastEvaluatedKey;
        }

        public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)items).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public int Count => items.Length;

        public T this[int index] => items[index];

        public PrimaryKey<T> LastEvaluatedKey => lastEvaluatedKey;
    }

    class WriteTransaction : IDynamoDBWriteTransaction
    {
        DynamoDBClient dynamoDBClient;
        List<TransactWriteItem> transactItems;
        bool isCommitted;

        public WriteTransaction(DynamoDBClient dynamoDBClient)
        {
            this.dynamoDBClient = dynamoDBClient;
            this.transactItems = new List<TransactWriteItem>();
        }

        public IDynamoDBWriteTransaction Put<T>(T item, Expression<Func<T, bool>>? condition = null) where T : class =>
            Add(new TransactWriteItem { Put = this.dynamoDBClient.CreatePutOperation(item, condition) });

        public IDynamoDBWriteTransaction Update<T>(PrimaryKey<T> key, Expression<Func<T, DynamoDBExpressions.UpdateAction>> update, Expression<Func<T, bool>>? condition = null, object? version = null) where T : class =>
            Add(new TransactWriteItem { Update = this.dynamoDBClient.CreateUpdateOperation(key, update, condition, version) });

        public IDynamoDBWriteTransaction Delete<T>(PrimaryKey<T> key, Expression<Func<T, bool>>? condition = null, object? version = null) where T : class =>
            Add(new TransactWriteItem { Delete = this.dynamoDBClient.CreateDeleteOperation(key, condition, version) });

        public IDynamoDBWriteTransaction ConditionCheck<T>(PrimaryKey<T> key, Expression<Func<T, bool>> condition, object? version = null) where T : class =>
            Add(new TransactWriteItem { ConditionCheck = this.dynamoDBClient.CreateConditionCheckOperation(key, condition, version) });

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            AssertIsNotCommitted();

            this.isCommitted = true;

            var client = this.dynamoDBClient.client;
            var request = 
                new TransactWriteItemsRequest 
                { 
                    TransactItems = this.transactItems
                };

            await this.dynamoDBClient.Invoke(client.TransactWriteItemsAsync, request, cancellationToken);
        }

        IDynamoDBWriteTransaction Add(TransactWriteItem item)
        {
            AssertIsNotCommitted();

            this.transactItems.Add(item);
            return this;
        }

        void AssertIsNotCommitted()
        {
            if (this.isCommitted)
                throw new InvalidOperationException("Transaction already committed");
        }
    }

    class CombinedDynamoDBItemEventHandlers(IEnumerable<IDynamoDBItemEventHandler> itemEventHandlers) : IDynamoDBItemEventHandler
    {
        T IDynamoDBItemEventHandler.OnItemDeserialized<T>(T item) => 
            itemEventHandlers.Aggregate(item, (value, handler) => handler.OnItemDeserialized(value));

        Dictionary<string, AttributeValue> IDynamoDBItemEventHandler.OnItemSerialized<T>(Dictionary<string, AttributeValue> item, ExpressionTranslationContext translationContext) =>
            itemEventHandlers.Aggregate(item, (value, handler) => handler.OnItemSerialized<T>(value, translationContext));

        string IDynamoDBItemEventHandler.OnItemUpdateTranslated<T>(string expression, object? version, ExpressionTranslationContext translationContext) =>
            itemEventHandlers.Aggregate(expression, (value, handler) => handler.OnItemUpdateTranslated<T>(value, version, translationContext));

        string? IDynamoDBItemEventHandler.OnItemConditionTranslated<T>(string? expression, object? version, ExpressionTranslationContext translationContext) =>
            itemEventHandlers.Aggregate(expression, (value, handler) => handler.OnItemConditionTranslated<T>(value, version, translationContext));
    }
}

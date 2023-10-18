using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;

using DynamoDB.Net.Exceptions;
using DynamoDB.Net.Serialization;
using DynamoDB.Net.Expressions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Newtonsoft.Json;


namespace DynamoDB.Net
{
    public class DynamoDBClient : IDynamoDBClient
    {
        IAmazonDynamoDB client;
        IOptions<DynamoDBClientOptions> options;
        IDynamoDBItemEventHandler itemEvents;
        ILogger<DynamoDBClient> logger;
        JsonSerializer serializer;


        public DynamoDBClient(
            IAmazonDynamoDB client, 
            IOptions<DynamoDBClientOptions> options, 
            IEnumerable<IDynamoDBItemEventHandler> itemEventHandlers, 
            ILogger<DynamoDBClient> logger)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));

            if (options == null)
                throw new ArgumentNullException(nameof(options));

            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            this.client = client;
            this.options = options;
            this.itemEvents = new DynamoDBItemEventHandlers(itemEventHandlers);
            this.logger = logger;
            this.serializer = JsonSerializer.Create(GetSerializerSettings());
        }


        JsonSerializerSettings GetSerializerSettings()
        {
            var settings = new JsonSerializerSettings();

            Options.ConfigureSerializer(settings, JsonContractResolver.DefaultDynamoDB);

            return settings;
        }

        DynamoDBClientOptions Options => options.Value;

        bool DefaultConsistentRead => Options?.DefaultConsistentRead ?? false;

        ReturnConsumedCapacity LogConsumedCapacity =>
            logger.IsEnabled(LogLevel.Debug)
                ? ReturnConsumedCapacity.INDEXES
                : ReturnConsumedCapacity.NONE;
        

        public Task<T> TryGetAsync<T>(
            PrimaryKey<T> key,
            bool? consistentRead = false,
            CancellationToken cancellationToken = default(CancellationToken)) where T : class =>
            GetAsync(key, consistentRead, cancellationToken, false);

        public Task<T> GetAsync<T>(
            PrimaryKey<T> key,
            bool? consistentRead = false,
            CancellationToken cancellationToken = default(CancellationToken)) where T : class =>
            GetAsync(key, consistentRead, cancellationToken, true);

        async Task<T> GetAsync<T>(
            PrimaryKey<T> key,
            bool? consistendRead,
            CancellationToken cancellationToken,
            bool throwErrorIfNotExists) where T : class
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            var request =
                new GetItemRequest
                {
                    TableName = Model.TableDescription.GetTableName<T>(Options),
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
            Expression<Func<T, bool>> condition = null,
            CancellationToken cancellationToken = default(CancellationToken)) where T : class
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            var expressionTranslationContext = new ExpressionTranslationContext<T>(serializer, Options.JsonWriterFlags);
            
            var version = Model.TableDescription.PropertyAccessors<T>.GetVersion?.Invoke(item);

            var serializedItem = itemEvents.OnItemSerialized(Serialize(item), expressionTranslationContext);
            var translatedItemCondition = itemEvents.OnItemConditionTranslated(condition?.Translate(expressionTranslationContext), version, expressionTranslationContext);

            var request =
                new PutItemRequest
                {
                    TableName = Model.TableDescription.GetTableName<T>(Options),
                    Item = serializedItem,
                    ConditionExpression = translatedItemCondition,
                    ExpressionAttributeNames = expressionTranslationContext.AttributeNames,
                    ExpressionAttributeValues = expressionTranslationContext.AttributeValues,
                    ReturnConsumedCapacity = LogConsumedCapacity,
                    ReturnValues = ReturnValue.NONE
                };

            await Invoke(client.PutItemAsync, request, cancellationToken);

            return DeserializeItem<T>(serializedItem);
        }

        public async Task<T> UpdateAsync<T>(
            PrimaryKey<T> key,
            Expression<Func<T, DynamoDBExpressions.UpdateAction>> update,
            Expression<Func<T, bool>> condition = null,
            object version = null,
            CancellationToken cancellationToken = default(CancellationToken)) where T : class
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            if (update == null)
                throw new ArgumentNullException(nameof(update));

            var expressionTranslationContext = new ExpressionTranslationContext<T>(serializer, Options.JsonWriterFlags);

            var translatedItemUpdate = itemEvents.OnItemUpdateTranslated(update.Translate(expressionTranslationContext), version, expressionTranslationContext);
            var translatedItemCondition = itemEvents.OnItemConditionTranslated(condition?.Translate(expressionTranslationContext), version, expressionTranslationContext);

            var request =
                new UpdateItemRequest
                {                    
                    TableName = Model.TableDescription.GetTableName<T>(Options),
                    Key = Serialize(key),
                    UpdateExpression = translatedItemUpdate,
                    ConditionExpression = translatedItemCondition,
                    ExpressionAttributeNames = expressionTranslationContext.AttributeNames,
                    ExpressionAttributeValues = expressionTranslationContext.AttributeValues,
                    ReturnConsumedCapacity = LogConsumedCapacity,
                    ReturnValues = ReturnValue.ALL_NEW
                };

             var response = await Invoke(client.UpdateItemAsync, request, cancellationToken);

            return DeserializeItem<T>(response.Attributes);
        }

        public Task DeleteAsync<T>(
            PrimaryKey<T> key,
            Expression<Func<T, bool>> condition = null,
            object version = null,
            CancellationToken cancellationToken = default(CancellationToken)) where T : class
        {
             if (key == null)
                throw new ArgumentNullException(nameof(key));

            var expressionTranslationContext = new ExpressionTranslationContext<T>(serializer, Options.JsonWriterFlags);

            var translatedItemCondition = itemEvents.OnItemConditionTranslated(condition?.Translate(expressionTranslationContext), version, expressionTranslationContext);

            var request =
                new DeleteItemRequest
                {
                    TableName = Model.TableDescription.GetTableName<T>(Options),
                    Key = Serialize(key),
                    ConditionExpression = translatedItemCondition,
                    ExpressionAttributeNames = expressionTranslationContext.AttributeNames,
                    ExpressionAttributeValues = expressionTranslationContext.AttributeValues,
                    ReturnConsumedCapacity = LogConsumedCapacity,
                    ReturnValues = ReturnValue.NONE
                };

            return Invoke(client.DeleteItemAsync, request, cancellationToken);
        }

        public async Task<IDynamoDBPartialResult<T>> ScanAsync<T>(
            Expression<Func<T, bool>> filter = null,
            PrimaryKey<T> exclusiveStartKey = default(PrimaryKey<T>),
            int? limit = null,
            bool? consistendRead = false,
            (string, string) index = default((string, string)),
            CancellationToken cancellationToken = default(CancellationToken)) where T : class
        {
            var expressionTranslationContext = new ExpressionTranslationContext<T>(serializer, Options.JsonWriterFlags);

            var request =
                new ScanRequest
                {
                    TableName = Model.TableDescription.GetTableName<T>(Options),
                    ExclusiveStartKey = Serialize(exclusiveStartKey),
                    IndexName = index.GetIndexName(expressionTranslationContext),
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
            Expression<Func<T, bool>> filter = null,
            PrimaryKey<T> exclusiveStartKey = default(PrimaryKey<T>),
            bool? scanIndexForward = null,
            int? limit = null,
            bool? consistentRead = false,
            (string, string) index = default((string, string)),
            CancellationToken cancellationToken = default(CancellationToken)) where T : class
        {
            if (keyCondition == null)
                throw new ArgumentNullException(nameof(keyCondition));

            var expressionTranslationContext = new ExpressionTranslationContext<T>(serializer, Options.JsonWriterFlags);

            var request =
                new QueryRequest
                {
                    TableName = Model.TableDescription.GetTableName<T>(Options),
                    ExclusiveStartKey = Serialize(exclusiveStartKey),
                    KeyConditionExpression = keyCondition.Translate(expressionTranslationContext),
                    IndexName = index != default((string, string)) 
                        ? index.GetIndexName(expressionTranslationContext) 
                        : keyCondition.GetIndexName(expressionTranslationContext),
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



        Dictionary<string, AttributeValue> Serialize<T>(T itemOrKeys) => 
            serializer.SerializeDynamoDBValue(itemOrKeys, Options.JsonWriterFlags).EnsureIsMSet().M;

        T Deserialize<T>(Dictionary<string, AttributeValue> attributes) =>
            serializer.DeserializeDynamoDBValue<T>(new AttributeValue { M = attributes, IsMSet = true });

        T DeserializeItem<T>(Dictionary<string, AttributeValue> attributes) where T : class =>
            itemEvents.OnItemDeserialized(Deserialize<T>(attributes));

        PrimaryKey<T> DeserializeKey<T>(Dictionary<string, AttributeValue> attributes) where T : class =>
            attributes.Count == 0 ? null : Deserialize<PrimaryKey<T>>(attributes);

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
            catch (AmazonDynamoDBException ex)
            {
                if (!(ex is ConditionalCheckFailedException))
                    logger.InvokeFailed(ex);
                    
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
    }
}
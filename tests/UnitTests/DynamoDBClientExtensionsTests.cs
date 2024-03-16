namespace DynamoDB.Net.Tests.UnitTests;

using System.Linq.Expressions;
using DynamoDB.Net.Model;
using DynamoDB.Net.Serialization.Newtonsoft.Json;

public class DynamoDBClientExtensionsTests
{
    static DynamoDBClientExtensionsTests()
    {
#pragma warning disable CA1806
        new JsonContractResolver(); // TODO: remove once TypeContractResolver.Default is populated by default
#pragma warning restore CA1806
    }

    Client client = new Client();

    [Fact]
    public async void CanGetAsync()
    {
        var key = new PrimaryKey<Item>("a", 1);
        var item = new Item { Partition = "a", Order = 1 };

        client.Items.Add(key, item);

        var actual = await DynamoDBClientExtensions.GetAsync(client, key);

        Assert.Same(item, actual);
    }

    [Fact]
    public async void CanTryGetAsync()
    {
        var key = new PrimaryKey<Item>("a", 1);
        var item = new Item { Partition = "a", Order = 1 };

        client.Items.Add(key, item);

        var actual = await DynamoDBClientExtensions.TryGetAsync(client, key);

        Assert.Same(item, actual);
    }

    [Fact]
    public async void CanPutAsync()
    {
        var key = new PrimaryKey<Item>("a", 1);
        var item = new Item { Partition = "a", Order = 1 };

        var actual = await DynamoDBClientExtensions.PutAsync(client, item);

        Assert.Same(item, actual);
        Assert.Same(item, client.Items[key]);
    }

    [Fact]
    public async void CanScanAsync()
    {
        var item1 = new Item { Partition = "a", Order = 1 };
        var item2 = new Item { Partition = "a", Order = 2 };
        var item3 = new Item { Partition = "b", Order = 1 };

        client.Items.Add(new PrimaryKey<Item>(item1), item1);
        client.Items.Add(new PrimaryKey<Item>(item2), item2);
        client.Items.Add(new PrimaryKey<Item>(item3), item3);

        var actual = await DynamoDBClientExtensions.ScanAsync(client, typeof (Item));

        Assert.Equal(new object[] { item1, item2, item3 }, actual);
    }

    [Fact]
    public async void CanScanRemainingAsync()
    {
        client.MaxPageSize = 2;

        var item1 = new Item { Partition = "a", Order = 1 };
        var item2 = new Item { Partition = "a", Order = 2 };
        var item3 = new Item { Partition = "b", Order = 1 };
        var item4 = new Item { Partition = "b", Order = 2 };

        client.Items.Add(new PrimaryKey<Item>(item1), item1);
        client.Items.Add(new PrimaryKey<Item>(item2), item2);
        client.Items.Add(new PrimaryKey<Item>(item3), item3);
        client.Items.Add(new PrimaryKey<Item>(item4), item4);

        var actual1 = await DynamoDBClientExtensions.ScanRemainingAsync<Item>(client);
        var actual2 = await DynamoDBClientExtensions.ScanRemainingAsync(client, typeof (Item));
        var actual3 = await DynamoDBClientExtensions.ScanRemainingAsync(client, typeof (Item), limit: 3);

        Assert.Equal(new object[] { item1, item2, item3, item4 }, actual1);
        Assert.Equal(new object[] { item1, item2, item3, item4 }, actual2);
        Assert.Equal(new object[] { item1, item2, item3 }, actual3);
    }

    [Fact]
    public async void CanQueryRemainingAsync()
    {
        client.MaxPageSize = 2;
        
        var item1 = new Item { Partition = "a", Order = 1 };
        var item2 = new Item { Partition = "a", Order = 2 };
        var item3 = new Item { Partition = "b", Order = 1 };
        var item4 = new Item { Partition = "b", Order = 2 };
        var item5 = new Item { Partition = "b", Order = 3 };

        client.Items.Add(new PrimaryKey<Item>(item1), item1);
        client.Items.Add(new PrimaryKey<Item>(item2), item2);
        client.Items.Add(new PrimaryKey<Item>(item3), item3);
        client.Items.Add(new PrimaryKey<Item>(item4), item4);
        client.Items.Add(new PrimaryKey<Item>(item5), item5);

        var actual = await DynamoDBClientExtensions.QueryRemainingAsync<Item>(client, item => item.Partition == "b");

        Assert.Equal(new object[] { item3, item4, item5 }, actual);
    }

    [Table]
    class Item
    {
        [PartitionKey]
        public required string Partition { get; set; }

        [SortKey]
        public int Order { get; set; }
    }

    class Client : IDynamoDBClient
    {
        public Dictionary<object, object> Items { get; } = new Dictionary<object, object>();
        public int MaxPageSize { get; set; } = int.MaxValue;

        public Task<T> GetAsync<T>(
            PrimaryKey<T> key, 
            bool? consistentRead = false,
            CancellationToken cancellationToken = default(CancellationToken)) where T : class =>
            Task.FromResult((T)Items[key]);
        
        public Task<T?> TryGetAsync<T>(
            PrimaryKey<T> key, 
            bool? consistentRead = false,
            CancellationToken cancellationToken = default(CancellationToken)) where T : class =>
            Task.FromResult((T?)Items.GetValueOrDefault(key));

       public Task<T> PutAsync<T>(
            T item, 
            Expression<Func<T, bool>>? condition = null, 
            CancellationToken cancellationToken = default(CancellationToken)) where T : class =>
            Task.FromResult((T)(Items[new PrimaryKey<T>(item)] = item));
        
        public Task<T> UpdateAsync<T>(
            PrimaryKey<T> key, 
            Expression<Func<T, DynamoDBExpressions.UpdateAction>> update, 
            Expression<Func<T, bool>>? condition = null, 
            object? version = null,
            CancellationToken cancellationToken = default(CancellationToken)) where T : class =>
            throw new NotImplementedException();

        public Task DeleteAsync<T>(
            PrimaryKey<T> key, 
            Expression<Func<T, bool>>? condition = null, 
            object? version = null,
            CancellationToken cancellationToken = default(CancellationToken)) where T : class =>
            throw new NotImplementedException();

        public Task<IDynamoDBPartialResult<T>> ScanAsync<T>(
            Expression<Func<T, bool>>? filter = null, 
            PrimaryKey<T> exclusiveStartKey = default(PrimaryKey<T>), 
            int? limit = null,
            bool? consistentRead = false,
            (string, string) index = default((string, string)),
            CancellationToken cancellationToken = default(CancellationToken)) where T : class =>
            QueryAsync(_ => true, exclusiveStartKey, limit);

        public Task<IDynamoDBPartialResult<T>> QueryAsync<T>(
            Expression<Func<T, bool>> keyCondition,
            Expression<Func<T, bool>>? filter = null, 
            PrimaryKey<T> exclusiveStartKey = default(PrimaryKey<T>), 
            bool? scanIndexForward = null,
            int? limit = null,
            bool? consistentRead = false,
            (string, string) index = default((string, string)),
            CancellationToken cancellationToken = default(CancellationToken)) where T : class =>
            QueryAsync(keyCondition.Compile(), exclusiveStartKey, limit);

        Task<IDynamoDBPartialResult<T>> QueryAsync<T>(Func<T, bool> condition, PrimaryKey<T> exclusiveStartKey, int? limit) where T : class =>
            Task.FromResult<IDynamoDBPartialResult<T>>(
                new PartialResult<T>(
                    Items.Values.
                        OfType<T>().
                        Where(condition).
                        SkipWhile(item => 
                            !exclusiveStartKey.Equals(default(PrimaryKey<T>)) &&
                            !exclusiveStartKey.Equals(new PrimaryKey<T>(item))).
                        Skip(exclusiveStartKey.Equals(default(PrimaryKey<T>)) ? 0 : 1).
                        Take(Math.Min(limit ?? int.MaxValue, MaxPageSize))));

        public IDynamoDBWriteTransaction BeginWriteTransaction() =>
            throw new NotImplementedException();
    }

    class PartialResult<T> : List<T>, IDynamoDBPartialResult<T> where T : class
    {
        public PartialResult(IEnumerable<T> collection) : base(collection) {}

        public PrimaryKey<T> LastEvaluatedKey => this.Count > 0 ? new PrimaryKey<T>(this.Last()) : default(PrimaryKey<T>);
    }
}

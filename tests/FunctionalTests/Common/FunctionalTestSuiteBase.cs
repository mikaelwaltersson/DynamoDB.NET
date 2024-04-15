using System.Reflection;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using DynamoDB.Net.Model;
using DynamoDB.Net.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;


namespace DynamoDB.Net.Tests.FunctionalTests.Common;

public abstract class FunctionalTestSuiteBase : IAsyncLifetime
{
    readonly HashSet<string> createdTables = [];
    
    protected FunctionalTestSuiteBase()
    {
        var services = new ServiceCollection();
            
        services.AddDefaultAWSOptions(
            new() 
            { 
                DefaultClientConfig = { ServiceURL = "http://localhost:8000" }
            });

        services.AddDynamoDBClient(
            options => 
            {
                options.TableNamePrefix = $"acceptance-tests-{Guid.NewGuid()}-";
            });

        services.Configure<DynamoDBSerializerOptions>(
            options => 
            {
                options.AttributeNameTransform = NameTransform.CamelCase;
                options.EnumValueNameTransform = NameTransform.CamelCase;
                options.SerializeDefaultValuesFor = type => type.IsEnum || type == typeof(decimal);
            });
            
        ServiceProvider = services.BuildServiceProvider();
        DynamoDB = ServiceProvider.GetRequiredService<IAmazonDynamoDB>();
        DynamoDBClient = ServiceProvider.GetRequiredService<IDynamoDBClient>();
        DynamoDBClientOptions = ServiceProvider.GetRequiredService<IOptions<DynamoDBClientOptions>>().Value;
        DynamoDBSerializer = ServiceProvider.GetRequiredService<IDynamoDBSerializer>();
    }

    protected IServiceProvider ServiceProvider { get; }

    protected IAmazonDynamoDB DynamoDB { get; }

    protected IDynamoDBClient DynamoDBClient { get; }

    protected DynamoDBClientOptions DynamoDBClientOptions { get; }

    protected IDynamoDBSerializer DynamoDBSerializer { get; }

    protected async Task<List<(string TableName, List<Dictionary<string, AttributeValue>> Items)>> GetAllStoredRawItemsAsync()
    {
        var result = new List<(string TableName, List<Dictionary<string, AttributeValue>> Items)>();

        foreach (var table in createdTables)
        {
            var response = await DynamoDB.ScanAsync(new() { TableName = table });
            
            if (response.Items.Count > 0)
                result.Add((table[DynamoDBClientOptions.TableNamePrefix.Length..], response.Items));
        }

        return result;
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        foreach (var type in DiscoverModelTypes())
        {
            var createTableRequest = 
                Model.TableDescription.Get(type)
                    .GetCreateTableRequest(DynamoDBSerializer, DynamoDBClientOptions);

            await DynamoDB.CreateTableAsync(createTableRequest);

            foreach (var item in DiscoverSeededItems(type))
            {
                await DynamoDB.PutItemAsync(
                    new() 
                    { 
                        TableName = createTableRequest.TableName, 
                        Item = DynamoDBSerializer.SerializeDynamoDBValue(item)!.M 
                    });   
            }

            createdTables.Add(createTableRequest.TableName);
        }
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        foreach (var tableName in createdTables)
            await DynamoDB.DeleteTableAsync(tableName);
    }

    static IEnumerable<Type> DiscoverModelTypes() =>
        typeof(FunctionalTestSuiteBase).Assembly.GetTypes()
            .Where(type => 
                type.HasCustomAttribute<TableAttribute>() && 
                type.Namespace!.StartsWith(typeof(FunctionalTestSuiteBase).Namespace!));

    IEnumerable<object> DiscoverSeededItems(Type type) =>
        from field in GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
        where field.HasCustomAttribute<SeedItemAttribute>() && type.IsAssignableFrom(field.FieldType) 
        select field.GetValue(this);

    [AttributeUsage(AttributeTargets.Field)]
    protected class SeedItemAttribute : Attribute
    {
    }
}

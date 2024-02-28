
using Amazon.DynamoDBv2;
using DynamoDB.Net.Model;
using DynamoDB.Net.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DynamoDB.Net.Tests.AcceptanceTests;

public partial class DynamoDBClientTests : IAsyncLifetime
{
    IServiceProvider serviceProvider = null!;
    IAmazonDynamoDB dynamoDB = null!;
    IDynamoDBClient dynamoDBClient = null!;
    string tableName = null!;

    async Task IAsyncLifetime.InitializeAsync()
    {
        var services = new ServiceCollection();

        services.AddOptions();
        services.AddLogging();
        
        services.AddSingleton(
            typeof(IAmazonDynamoDB),
            serviceProvider =>
                new AmazonDynamoDBClient(
                    new AmazonDynamoDBConfig
                    {
                        ServiceURL = "http://localhost:8000"
                    }));

        services.AddSingleton<IDynamoDBClient, DynamoDBClient>();
        services.Configure<DynamoDBClientOptions>(
            options => 
            {
                options.TableNamePrefix = $"acceptance-tests-{Guid.NewGuid()}";
            }
        );

        serviceProvider = services.BuildServiceProvider();

        dynamoDB = serviceProvider.GetRequiredService<IAmazonDynamoDB>();
        
        var createTableRequest = 
            TableDescription.Get(
                typeof(TestModels.UserPost), 
                JsonContractResolver.DefaultDynamoDB).
                    GetCreateTableRequest(
                        serviceProvider.GetRequiredService<IOptions<DynamoDBClientOptions>>().Value);

        await dynamoDB.CreateTableAsync(createTableRequest);

        dynamoDBClient = serviceProvider.GetRequiredService<IDynamoDBClient>();
        tableName = createTableRequest.TableName;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        var dynamoDb = serviceProvider.GetRequiredService<IAmazonDynamoDB>();
        
        await dynamoDb.DeleteTableAsync(tableName);
    }
}

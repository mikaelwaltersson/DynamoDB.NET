
using Amazon.DynamoDBv2;
using Amazon.Extensions.NETCore.Setup;
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
            
        services.AddDefaultAWSOptions(
            new() 
            { 
                DefaultClientConfig = { ServiceURL = "http://localhost:8000" }
            });

        services.AddSingleton<IDynamoDBSerializer, Serialization.Newtonsoft.Json.JsonDynamoDBSerializer>();
        
        services.AddDynamoDBClient(
            options => 
            {
                options.TableNamePrefix = $"acceptance-tests-{Guid.NewGuid()}";
            });

        serviceProvider = services.BuildServiceProvider();

        dynamoDB = serviceProvider.GetRequiredService<IAmazonDynamoDB>();
        
        var createTableRequest = 
            TableDescription.Get(typeof(TestModels.UserPost)).GetCreateTableRequest(
                serviceProvider.GetRequiredService<IDynamoDBSerializer>(),
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

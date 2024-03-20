using Amazon.DynamoDBv2;
using DynamoDB.Net.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DynamoDB.Net;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDynamoDBClient(this IServiceCollection services, Action<DynamoDBClientOptions>? configureOptions = null)
    {
        services.AddOptions();
        services.AddLogging();

        services.TryAddAWSService<IAmazonDynamoDB>();
        services.TryAddSingleton<IDynamoDBSerializer, DynamoDBSerializer>();

        services.AddScoped<IDynamoDBClient, DynamoDBClient>();
        services.TryAddSingleton<IDynamoDBItemEventHandler, VersionChecker>();
        
        if (configureOptions != null)
            services.Configure(configureOptions);

        return services;
    }
}

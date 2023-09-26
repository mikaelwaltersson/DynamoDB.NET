using Amazon.DynamoDBv2;
using DynamoDB.Net;
using DynamoDB.Net.Model;
using DynamoDB.Net.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Snapshooter.Xunit;
using Xunit;

namespace DynamoDB.NET.Tests.AcceptanceTests
{
    public partial class DynamoDBClientTests
    {
        [Fact]
        public async Task CanCreateTables()
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
                    options.TableNamePrefix = "acceptance-tests-";
                }
            );

            var serviceProvider = services.BuildServiceProvider();

            var dynamoDb = serviceProvider.GetRequiredService<IAmazonDynamoDB>();
            var dynamoDbClientOptions = serviceProvider.GetRequiredService<IOptions<DynamoDBClientOptions>>();
            var client = serviceProvider.GetRequiredService<IDynamoDBClient>();

            var table = TableDescription.Get(typeof(TestModels.UserPost), JsonContractResolver.DefaultDynamoDB);
            var createTableRequest = table.GetCreateTableRequest(dynamoDbClientOptions.Value);
            
            var result = await dynamoDb.CreateTableAsync(createTableRequest);
            try 
            {
                Assert.Equal("acceptance-tests-user-posts", result.TableDescription.TableName);

                var item1 = await client.PutAsync(
                    new TestModels.UserPost 
                    { 
                        UserId = new Guid("00000000-0000-0000-0000-000000000001"), 
                        Timestamp = new DateTime(2022, 10, 18, 16, 42, 0),
                        RoleIds =
                        {
                            new Guid("00000000-0000-0000-0000-000000000002"), 
                            new Guid("00000000-0000-0000-0000-000000000003")
                        }
                    });
                var item2 = await client.TryGetAsync(new PrimaryKey<TestModels.UserPost>(new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2022, 10, 18, 16, 42, 0)));

                Assert.NotNull(item2);
                Snapshot.Match(item1);
            }
            finally 
            {
                await dynamoDb.DeleteTableAsync(result.TableDescription.TableName);
            }
        }
    }
}
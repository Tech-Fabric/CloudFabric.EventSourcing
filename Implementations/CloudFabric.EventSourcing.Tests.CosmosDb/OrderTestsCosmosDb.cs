using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using CloudFabric.EventSourcing.EventStore;
using CloudFabric.EventSourcing.EventStore.CosmosDb;
using CloudFabric.Projections;
using CloudFabric.Projections.CosmosDb;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CloudFabric.EventSourcing.Tests.CosmosDb;

[TestClass]
public class OrderTestsCosmosDb : OrderTests
{
    private const string DatabaseName = "TestDatabase";
    private const string ContainerName = "TestContainer";
    private const string ProjectionsContainerName = "TestProjectionsContainer";

    /**
         * Lease database and container are used to synchonize all changefeed readers
         */
    private const string LeaseDatabaseName = "TestDatabaseLease";

    /**
         * Lease database and container are used to synchonize all changefeed readers
         */
    private const string LeaseContainerName = "TestContainerLease";

    private const string CosmosDbConnectionString =
        "AccountEndpoint=https://fiber-eventsourcing-test.documents.azure.com:443/;AccountKey=Lf26olS0XqskqAoOpPaWDrc5me1e3W3hwxXfi3WmWWVk39z5s6JzdAmzLfQHvAq7TGntgxmen233PS9tK3J4vA==;";

    private readonly Dictionary<Type, object> _projectionsRepositories = new();

    CosmosClient _cosmosClient = null;
    CosmosClientOptions _cosmosClientOptions;

    private IEventStore? _eventStore = null;
    private CosmosDbEventStoreChangeFeedObserver? _eventStoreEventsObserver = null;
    private ILogger _logger;

    [TestInitialize]
    public async Task SetUp()
    {
        ProjectionsUpdateDelay = TimeSpan.FromSeconds(30);

        var loggerFactory = new LoggerFactory();
        _logger = loggerFactory.CreateLogger<OrderTestsCosmosDb>();

        JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        CosmosDbSystemTextJsonSerializer cosmosSystemTextJsonSerializer
            = new CosmosDbSystemTextJsonSerializer(jsonSerializerOptions);

        _cosmosClientOptions = new CosmosClientOptions()
        {
            Serializer = cosmosSystemTextJsonSerializer,
            HttpClientFactory = () =>
            {
                HttpMessageHandler httpMessageHandler = new HttpClientHandler()
                {
                    ServerCertificateCustomValidationCallback =
                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };

                return new HttpClient(httpMessageHandler);
            },
            ConnectionMode = ConnectionMode.Gateway
        };

        _cosmosClient = new CosmosClient(
            CosmosDbConnectionString,
            _cosmosClientOptions
        );


        var database = await ReCreateDatabase(_cosmosClient, DatabaseName);
        await database.CreateContainerIfNotExistsAsync(new ContainerProperties(ContainerName, "/stream/id"));
        await database.CreateContainerIfNotExistsAsync(
            new ContainerProperties(
                ProjectionsContainerName,
                "/partitionKey"
            )
        );
        ContainerResponse containerResponse =
            await _cosmosClient.GetContainer(DatabaseName, ContainerName).ReadContainerAsync();
        // Set the indexing mode to consistent
        containerResponse.Resource.IndexingPolicy.IndexingMode = IndexingMode.Consistent;
        // Add an included path
        containerResponse.Resource.IndexingPolicy.IncludedPaths.Add(new IncludedPath { Path = "/id" });
        containerResponse.Resource.IndexingPolicy.IncludedPaths.Add(new IncludedPath { Path = "/stream/*" });
        // Add an excluded path
        containerResponse.Resource.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = "/eventData/*" });


        var leaseDatabase = await ReCreateDatabase(_cosmosClient, LeaseDatabaseName);
        await leaseDatabase.CreateContainerIfNotExistsAsync(
            new ContainerProperties(LeaseContainerName, "/partitionKey")
        );

        // add stored procedure
        var migration = new CosmosDbEventStoreMigration(_cosmosClient, DatabaseName, ContainerName);
        await migration.RunAsync();
    }

    private async Task<Database> ReCreateDatabase(CosmosClient cosmosClient, string databaseName)
    {
        await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseName);
        var database = cosmosClient.GetDatabase(databaseName);
        await database.DeleteAsync();
        await cosmosClient.CreateDatabaseIfNotExistsAsync(
            databaseName,
            ThroughputProperties.CreateManualThroughput(400)
        );
        return cosmosClient.GetDatabase(databaseName);
    }

    protected override async Task<IEventStore> GetEventStore()
    {
        if (_eventStore == null)
        {
            _eventStore = new CosmosDbEventStore(
                _cosmosClient,
                DatabaseName,
                ContainerName
            );
            await _eventStore.Initialize();
        }

        return _eventStore;
    }

    protected override IEventsObserver GetEventStoreEventsObserver()
    {
        if (_eventStoreEventsObserver == null)
        {
            _eventStoreEventsObserver = new CosmosDbEventStoreChangeFeedObserver(
                _cosmosClient,
                DatabaseName,
                ContainerName,
                _cosmosClient,
                LeaseDatabaseName,
                LeaseContainerName,
                ""
            );
        }

        return _eventStoreEventsObserver;
    }

    protected override IProjectionRepository<T> GetProjectionRepository<T>()
    {
        if (!_projectionsRepositories.ContainsKey(typeof(T)))
        {
            _projectionsRepositories[typeof(T)] = new CosmosDbProjectionRepository<T>(
                new LoggerFactory(),
                CosmosDbConnectionString,
                _cosmosClientOptions,
                DatabaseName,
                ProjectionsContainerName,
                $"projection-{typeof(T).Name}"
            );
        }

        return (IProjectionRepository<T>)_projectionsRepositories[typeof(T)];
    }

    protected override IProjectionRepository<ProjectionRebuildState> GetProjectionRebuildStateRepository()
    {
        return new CosmosDbProjectionRepository<ProjectionRebuildState>(
            new LoggerFactory(),
            CosmosDbConnectionString,
            _cosmosClientOptions,
            DatabaseName,
            ProjectionsContainerName,
            partitionKey: "ProjectionRebuildState"
        );
    }

    public async Task LoadTest()
    {
        var watch = Stopwatch.StartNew();

        var tasks = new List<Task>();

        for (var i = 0; i < 100; i++)
        {
            for (var j = 0; j < 10; j++)
            {
                tasks.Add(TestPlaceOrderAndAddItem());
            }
        }

        await Task.WhenAll(tasks);

        watch.Stop();

        Console.WriteLine($"It took {watch.Elapsed}!");
    }
}

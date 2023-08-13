using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace CloudFabric.Projections.CosmosDb;

public class CosmosDbProjectionRepositoryFactory : ProjectionRepositoryFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly string _connectionString;
    private readonly CosmosClientOptions _cosmosClientOptions;
    private readonly string _databaseId;
    private readonly string _containerId;

    public CosmosDbProjectionRepositoryFactory(
        ILoggerFactory loggerFactory,
        string connectionString,
        CosmosClientOptions cosmosClientOptions,
        string databaseId,
        string containerId
    ): base(loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _connectionString = connectionString;
        _cosmosClientOptions = cosmosClientOptions;
        _databaseId = databaseId;
        _containerId = containerId;
    }

    public override IProjectionRepository<TProjectionDocument> GetProjectionRepository<TProjectionDocument>()
    {
        var cached = GetFromCache<TProjectionDocument>();
        if (cached != null)
        {
            return cached;
        }

        var repository = new CosmosDbProjectionRepository<TProjectionDocument>(
            _loggerFactory,
            _connectionString,
            _cosmosClientOptions,
            _databaseId,
            _containerId
        );

        SetToCache<TProjectionDocument>(repository);
        return repository;
    }

    public override ProjectionRepository GetProjectionRepository(ProjectionDocumentSchema projectionDocumentSchema)
    {
        var cached = GetFromCache(projectionDocumentSchema);
        if (cached != null)
        {
            return cached;
        }

        var repository = new CosmosDbProjectionRepository(
            _loggerFactory,
            _connectionString,
            _cosmosClientOptions,
            _databaseId,
            _containerId,
            projectionDocumentSchema
        );

        SetToCache(projectionDocumentSchema, repository);
        return repository;
    }
}
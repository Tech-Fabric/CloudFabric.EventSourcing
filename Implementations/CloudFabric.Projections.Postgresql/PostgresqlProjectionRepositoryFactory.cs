using Microsoft.Extensions.Logging;

namespace CloudFabric.Projections.Postgresql;

public class PostgresqlProjectionRepositoryFactory: ProjectionRepositoryFactory
{
    private readonly string _projectionsConnectionString;
    private readonly bool _includeDebugInformation;
    private readonly string? _sourceEventStoreConnectionId;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="loggerFactory">
    /// </param>
    /// <param name="projectionsConnectionString">
    /// Projections store connection string.
    /// </param>
    /// <param name="sourceEventStoreConnectionId">
    /// Connection id if the event store - source of projections.
    /// Since there can be multiple source event stores in multi tenant applications or load-balanced applications,
    /// we need to store some kind of id to be able to restore it later for projections rebuild.
    /// </param>
    public PostgresqlProjectionRepositoryFactory(
        ILoggerFactory loggerFactory,
        string projectionsConnectionString,
        string? sourceEventStoreConnectionId = null,
        bool includeDebugInformation = false
    ): base(loggerFactory)
    {
        _projectionsConnectionString = projectionsConnectionString;
        _sourceEventStoreConnectionId = sourceEventStoreConnectionId;
        _includeDebugInformation = includeDebugInformation;
    }
    
    public override IProjectionRepository<TProjectionDocument> GetProjectionRepository<TProjectionDocument>()
    {
        var cached = GetFromCache<TProjectionDocument>();
        if (cached != null)
        {
            return cached;
        }
        
        var repository = new PostgresqlProjectionRepository<TProjectionDocument>(
            _projectionsConnectionString, _loggerFactory, _includeDebugInformation
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
        
        var repository = new PostgresqlProjectionRepository(
            _projectionsConnectionString, 
            projectionDocumentSchema,
            _loggerFactory,
            _includeDebugInformation
        );
        SetToCache(projectionDocumentSchema, repository);
        return repository;
    }
}
using Microsoft.Extensions.Logging;

namespace CloudFabric.Projections.InMemory;

public class InMemoryProjectionRepositoryFactory : ProjectionRepositoryFactory
{
    public InMemoryProjectionRepositoryFactory(ILoggerFactory loggerFactory): base(loggerFactory)
    {
    }

    public override IProjectionRepository<TProjectionDocument> GetProjectionRepository<TProjectionDocument>()
    {
        var cached = GetFromCache<TProjectionDocument>();
        if (cached != null)
        {
            return cached;
        }
        
        var repository = new InMemoryProjectionRepository<TProjectionDocument>(_loggerFactory);
        
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

        var repository = new InMemoryProjectionRepository(projectionDocumentSchema, _loggerFactory);

        SetToCache(projectionDocumentSchema, repository);
        return repository;
    }
}
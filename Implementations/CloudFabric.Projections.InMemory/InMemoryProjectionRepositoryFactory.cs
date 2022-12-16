namespace CloudFabric.Projections.InMemory;

public class InMemoryProjectionRepositoryFactory : ProjectionRepositoryFactory
{
    public override IProjectionRepository<TProjectionDocument> GetProjectionRepository<TProjectionDocument>()
    {
        var cached = GetFromCache<TProjectionDocument>();
        if (cached != null)
        {
            return cached;
        }
        
        var repository = new InMemoryProjectionRepository<TProjectionDocument>();
        
        SetToCache<TProjectionDocument>(repository);
        return repository;
    }

    public override IProjectionRepository GetProjectionRepository(ProjectionDocumentSchema projectionDocumentSchema)
    {
        var cached = GetFromCache(projectionDocumentSchema);
        if (cached != null)
        {
            return cached;
        }

        var repository = new InMemoryProjectionRepository(projectionDocumentSchema);

        SetToCache(projectionDocumentSchema, repository);
        return repository;
    }
}

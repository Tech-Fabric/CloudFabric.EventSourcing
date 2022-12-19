namespace CloudFabric.Projections.Postgresql;

public class PostgresqlProjectionRepositoryFactory: ProjectionRepositoryFactory
{
    private readonly string _connectionString;
    
    public PostgresqlProjectionRepositoryFactory(string connectionString)
    {
        _connectionString = connectionString;
    }
    
    public override IProjectionRepository<TProjectionDocument> GetProjectionRepository<TProjectionDocument>()
    {
        var cached = GetFromCache<TProjectionDocument>();
        if (cached != null)
        {
            return cached;
        }
        
        var repository = new PostgresqlProjectionRepository<TProjectionDocument>(_connectionString);
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
        
        var repository = new PostgresqlProjectionRepository(_connectionString, projectionDocumentSchema);
        SetToCache(projectionDocumentSchema, repository);
        return repository;
    }
}

namespace CloudFabric.Projections;

public abstract class ProjectionRepositoryFactory
{
    private readonly Dictionary<string, object> _repositories = new Dictionary<string, object>();

    protected IProjectionRepository<TProjectionDocument>? GetFromCache<TProjectionDocument>() where TProjectionDocument : ProjectionDocument
    {
        var name = typeof(TProjectionDocument).FullName!;
        
        if (_repositories.ContainsKey(name))
        {
            return (IProjectionRepository<TProjectionDocument>)_repositories[name];
        }

        return null;
    }

    protected void SetToCache<TProjectionDocument>(IProjectionRepository<TProjectionDocument> repository) where TProjectionDocument : ProjectionDocument
    {
        var name = typeof(TProjectionDocument).FullName!;

        _repositories[name] = repository;
    }
    
    protected IProjectionRepository? GetFromCache(ProjectionDocumentSchema schema)
    {
        var name = $"{schema.SchemaName}_{ProjectionDocumentSchemaFactory.GetPropertiesUniqueHash(schema.Properties)}";
        
        if (_repositories.ContainsKey(name))
        {
            return (IProjectionRepository)_repositories[name];
        }

        return null;
    }

    protected void SetToCache(ProjectionDocumentSchema schema, IProjectionRepository repository)
    {
        var name = $"{schema.SchemaName}_{ProjectionDocumentSchemaFactory.GetPropertiesUniqueHash(schema.Properties)}";

        _repositories[name] = repository;
    }
    
    public abstract IProjectionRepository<TProjectionDocument> GetProjectionRepository<TProjectionDocument>()
        where TProjectionDocument : ProjectionDocument;

    public abstract IProjectionRepository GetProjectionRepository(ProjectionDocumentSchema projectionDocumentSchema);
}

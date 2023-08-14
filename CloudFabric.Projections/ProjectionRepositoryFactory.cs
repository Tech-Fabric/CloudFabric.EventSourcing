using Microsoft.Extensions.Logging;

namespace CloudFabric.Projections;

public abstract class ProjectionRepositoryFactory
{
    protected readonly Dictionary<string, object> _repositories = new Dictionary<string, object>();

    protected readonly ILoggerFactory _loggerFactory;

    public ProjectionRepositoryFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    protected IProjectionRepository<TProjectionDocument>? GetFromCache<TProjectionDocument>() where TProjectionDocument : ProjectionDocument
    {
        var name = typeof(TProjectionDocument).FullName!;
        
        if (_repositories.ContainsKey(name))
        {
            return (IProjectionRepository<TProjectionDocument>)_repositories[name];
        }

        return null;
    }

    protected virtual void SetToCache<TProjectionDocument>(IProjectionRepository<TProjectionDocument> repository) where TProjectionDocument : ProjectionDocument
    {
        var name = typeof(TProjectionDocument).FullName!;

        _repositories[name] = repository;
    }
    
    protected virtual ProjectionRepository? GetFromCache(ProjectionDocumentSchema? schema)
    {
        var name = "empty-schema";
        
        if (schema != null)
        {
            name = $"{schema.SchemaName}_{ProjectionDocumentSchemaFactory.GetPropertiesUniqueHash(schema.Properties)}";
        }

        if (_repositories.ContainsKey(name))
        {
            return (ProjectionRepository)_repositories[name];
        }

        return null;
    }

    protected virtual void SetToCache(ProjectionDocumentSchema? schema, ProjectionRepository repository)
    {
        var name = "empty-schema";
        
        if (schema != null)
        {
            name = $"{schema.SchemaName}_{ProjectionDocumentSchemaFactory.GetPropertiesUniqueHash(schema.Properties)}";
        }

        _repositories[name] = repository;
    }
    
    public abstract IProjectionRepository<TProjectionDocument> GetProjectionRepository<TProjectionDocument>()
        where TProjectionDocument : ProjectionDocument;

    public abstract ProjectionRepository GetProjectionRepository(ProjectionDocumentSchema projectionDocumentSchema);

    public ProjectionRepository GetProjectionsIndexStateRepository()
    {
        return GetProjectionRepository(ProjectionDocumentSchemaFactory.FromTypeWithAttributes<ProjectionIndexState>());
    }
}
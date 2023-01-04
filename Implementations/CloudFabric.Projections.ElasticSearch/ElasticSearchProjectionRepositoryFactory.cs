using Microsoft.Extensions.Logging;

namespace CloudFabric.Projections.ElasticSearch;

public class ElasticSearchProjectionRepositoryFactory : ProjectionRepositoryFactory
{
    private readonly string _uri;
    private readonly string _username;
    private readonly string _password;
    private readonly string _certificateThumbprint;
    private readonly ILoggerFactory _loggerFactory;
    
    public ElasticSearchProjectionRepositoryFactory(
        string uri,
        string username,
        string password,
        string certificateFingerprint,
        ILoggerFactory loggerFactory
    )
    {
        _uri = uri;
        _username = username;
        _password = password;
        _certificateThumbprint = certificateFingerprint;
        _loggerFactory = loggerFactory;
    }
    
    public override IProjectionRepository<TProjectionDocument> GetProjectionRepository<TProjectionDocument>()
    {
        var cached = GetFromCache<TProjectionDocument>();
        if (cached != null)
        {
            return cached;
        }
        
        var repository = new ElasticSearchProjectionRepository<TProjectionDocument>(
            _uri,
            _username,
            _password,
            _certificateThumbprint,
            _loggerFactory
        );
        
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
        
        var repository = new ElasticSearchProjectionRepository(
            _uri,
            _username,
            _password,
            _certificateThumbprint,
            projectionDocumentSchema,
            _loggerFactory
        );
        
        SetToCache(projectionDocumentSchema, repository);
        return repository;
    }
}

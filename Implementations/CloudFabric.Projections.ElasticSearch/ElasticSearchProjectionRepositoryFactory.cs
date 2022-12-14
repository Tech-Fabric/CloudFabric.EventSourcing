using Microsoft.Extensions.Logging;

namespace CloudFabric.Projections.ElasticSearch;

public class ElasticSearchProjectionRepositoryFactory : ProjectionRepositoryFactory
{
    private readonly string _uri;
    private readonly string _username;
    private readonly string _password;
    private readonly string _certificateThumbprint;
    private readonly LoggerFactory _loggerFactory;
    
    public ElasticSearchProjectionRepositoryFactory(
        string uri,
        string username,
        string password,
        string certificateFingerprint,
        LoggerFactory loggerFactory
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
        return new ElasticSearchProjectionRepository<TProjectionDocument>(
            _uri,
            _username,
            _password,
            _certificateThumbprint,
            _loggerFactory
        );
    }

    public override IProjectionRepository GetProjectionRepository(ProjectionDocumentSchema projectionDocumentSchema)
    {
        return new ElasticSearchProjectionRepository(
            _uri,
            _username,
            _password,
            _certificateThumbprint,
            projectionDocumentSchema,
            _loggerFactory
        );
    }
}

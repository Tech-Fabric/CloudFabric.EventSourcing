using Elasticsearch.Net;
using Microsoft.Extensions.Logging;

namespace CloudFabric.Projections.ElasticSearch;

public class ElasticSearchProjectionRepositoryFactory : ProjectionRepositoryFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ElasticSearchBasicAuthConnectionSettings? _basicAuthConnectionSettings;
    private readonly ElasticSearchApiKeyAuthConnectionSettings? _apiKeyAuthConnectionSettings;
    public ElasticSearchProjectionRepositoryFactory(
        ElasticSearchBasicAuthConnectionSettings connectionSettings,
        ILoggerFactory loggerFactory
    )
    {
        _basicAuthConnectionSettings = connectionSettings;
        _loggerFactory = loggerFactory;
    }

    public ElasticSearchProjectionRepositoryFactory(ElasticSearchApiKeyAuthConnectionSettings apiKeyAuthConnectionSettings, ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _apiKeyAuthConnectionSettings = apiKeyAuthConnectionSettings;
    }

    public override IProjectionRepository<TProjectionDocument> GetProjectionRepository<TProjectionDocument>()
    {
        var cached = GetFromCache<TProjectionDocument>();
        if (cached != null)
        {
            return cached;
        }

        IProjectionRepository<TProjectionDocument>? repository = null;
        if (_basicAuthConnectionSettings != null)
        {
            repository = new ElasticSearchProjectionRepository<TProjectionDocument>(_basicAuthConnectionSettings,
                _loggerFactory
            );
        }
        else if (_apiKeyAuthConnectionSettings != null)
        {
            repository = new ElasticSearchProjectionRepository<TProjectionDocument>(_apiKeyAuthConnectionSettings, _loggerFactory);
        }

        if (repository != null)
        {
            SetToCache(repository);
            return repository;
        }

        throw new Exception("Missed Elastic connection settings");
    }

    public override IProjectionRepository GetProjectionRepository(ProjectionDocumentSchema projectionDocumentSchema)
    {
        var cached = GetFromCache(projectionDocumentSchema);
        if (cached != null)
        {
            return cached;
        }
        
        IProjectionRepository? repository = null;
        if (_basicAuthConnectionSettings != null)
        {
            repository = new ElasticSearchProjectionRepository(_basicAuthConnectionSettings,
                projectionDocumentSchema,
                _loggerFactory
            );
        }
        else if (_apiKeyAuthConnectionSettings != null)
        {
            repository = new ElasticSearchProjectionRepository(_apiKeyAuthConnectionSettings,
                projectionDocumentSchema,
                _loggerFactory);
        }

        if (repository != null)
        {
            SetToCache(projectionDocumentSchema, repository);
            return repository;
        }
        throw new Exception("Missed Elastic connection settings");
    }
}

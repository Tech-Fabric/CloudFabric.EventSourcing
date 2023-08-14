using Microsoft.Extensions.Logging;

namespace CloudFabric.Projections.ElasticSearch;

public class ElasticSearchProjectionRepositoryFactory : ProjectionRepositoryFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ElasticSearchBasicAuthConnectionSettings? _basicAuthConnectionSettings;
    private readonly ElasticSearchApiKeyAuthConnectionSettings? _apiKeyAuthConnectionSettings;
    private readonly bool _disableRequestStreaming;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="connectionSettings"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="disableRequestStreaming">
    /// When request streaming is disabled, elastic adds debug information about request and response to response object which can
    /// be useful when troubleshooting search problems.
    ///
    /// Defaults to false to improve performance.
    /// </param>
    public ElasticSearchProjectionRepositoryFactory(
        ElasticSearchBasicAuthConnectionSettings connectionSettings,
        ILoggerFactory loggerFactory,
        bool disableRequestStreaming = false
    ): base(loggerFactory)
    {
        _basicAuthConnectionSettings = connectionSettings;
        _loggerFactory = loggerFactory;
        _disableRequestStreaming = disableRequestStreaming;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="apiKeyAuthConnectionSettings"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="disableRequestStreaming">
    /// When request streaming is disabled, elastic adds debug information about request and response to response object which can
    /// be useful when troubleshooting search problems.
    ///
    /// Defaults to false to improve performance.
    /// </param>
    public ElasticSearchProjectionRepositoryFactory(
        ElasticSearchApiKeyAuthConnectionSettings apiKeyAuthConnectionSettings, 
        ILoggerFactory loggerFactory,
        bool disableRequestStreaming = false
    ): base(loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _apiKeyAuthConnectionSettings = apiKeyAuthConnectionSettings;
        _disableRequestStreaming = disableRequestStreaming;
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
            repository = new ElasticSearchProjectionRepository<TProjectionDocument>(
                _basicAuthConnectionSettings,
                _loggerFactory,
                _disableRequestStreaming
            );
        }
        else if (_apiKeyAuthConnectionSettings != null)
        {
            repository = new ElasticSearchProjectionRepository<TProjectionDocument>(
                _apiKeyAuthConnectionSettings, _loggerFactory, _disableRequestStreaming
            );
        }

        if (repository != null)
        {
            SetToCache(repository);
            return repository;
        }

        throw new Exception("Missed Elastic connection settings");
    }

    public override ProjectionRepository GetProjectionRepository(ProjectionDocumentSchema projectionDocumentSchema)
    {
        var cached = GetFromCache(projectionDocumentSchema);
        if (cached != null)
        {
            return cached;
        }
        
        ProjectionRepository? repository = null;
        if (_basicAuthConnectionSettings != null)
        {
            repository = new ElasticSearchProjectionRepository(
                _basicAuthConnectionSettings,
                projectionDocumentSchema,
                _loggerFactory,
                _disableRequestStreaming
            );
        }
        else if (_apiKeyAuthConnectionSettings != null)
        {
            repository = new ElasticSearchProjectionRepository(
                _apiKeyAuthConnectionSettings,
                projectionDocumentSchema,
                _loggerFactory,
                _disableRequestStreaming
            );
        }

        if (repository != null)
        {
            SetToCache(projectionDocumentSchema, repository);
            return repository;
        }
        throw new Exception("Missed Elastic connection settings");
    }
}
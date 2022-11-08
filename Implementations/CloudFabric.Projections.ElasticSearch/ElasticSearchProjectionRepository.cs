using System.Linq;
using System.Security.AccessControl;
using System.Text.Json;
using CloudFabric.Projections.ElasticSearch.Helpers;
using CloudFabric.Projections.Queries;
using CloudFabric.Projections.Utils;
using Microsoft.Extensions.Logging;
using Nest;

namespace CloudFabric.Projections.ElasticSearch;

public class ElasticSearchProjectionRepository<TProjectionDocument> : ElasticSearchProjectionRepository, IProjectionRepository<TProjectionDocument>
    where TProjectionDocument : ProjectionDocument
{
    public ElasticSearchProjectionRepository(
        string uri,
        string username,
        string password,
        string certificateFingerprint,
        LoggerFactory loggerFactory
    ) : base(
        uri,
        username,
        password,
        certificateFingerprint,
        ProjectionDocumentSchemaFactory.FromTypeWithAttributes<TProjectionDocument>(),
        loggerFactory
    )
    {
    }

    public new async Task<TProjectionDocument?> Single(Guid id, string partitionKey, CancellationToken cancellationToken)
    {
        var document = await base.Single(id, partitionKey, cancellationToken);

        if (document == null)
        {
            return null;
        }

        return ProjectionDocumentSerializer.DeserializeFromDictionary<TProjectionDocument>(document);
    }

    public Task Upsert(TProjectionDocument document, string partitionKey, CancellationToken cancellationToken = default)
    {
        var documentDictionary = ProjectionDocumentSerializer.SerializeToDictionary(document);
        return Upsert(documentDictionary, partitionKey, cancellationToken);
    }

    public new async Task<IReadOnlyCollection<TProjectionDocument>> Query(ProjectionQuery projectionQuery, string? partitionKey = null, CancellationToken cancellationToken = default)
    {
        var recordsDictionary = await base.Query(projectionQuery, partitionKey, cancellationToken);

        var records = new List<TProjectionDocument>();

        foreach (var dict in recordsDictionary)
        {
            records.Add(
                ProjectionDocumentSerializer.DeserializeFromDictionary<TProjectionDocument>(dict)
            );
        }

        return records;
    }
}

public class ElasticSearchProjectionRepository : IProjectionRepository
{
    private readonly ProjectionDocumentSchema _projectionDocumentSchema;
    private string? _keyPropertyName;
    private string? _indexName;
    private readonly ElasticClient _client;
    private readonly ElasticSearchIndexer _indexer;
    private readonly ILogger<ElasticSearchProjectionRepository> _logger;

    public ElasticSearchProjectionRepository(
        string uri,
        string username,
        string password,
        string certificateFingerprint,
        ProjectionDocumentSchema projectionDocumentSchema,
        LoggerFactory loggerFactory
    )
    {
        _projectionDocumentSchema = projectionDocumentSchema;
        _logger = loggerFactory.CreateLogger<ElasticSearchProjectionRepository>();

        var connectionSettings = new ConnectionSettings(new Uri(uri));
        connectionSettings.BasicAuthentication(username, password);
        connectionSettings.CertificateFingerprint(certificateFingerprint);
        connectionSettings.DefaultIndex(IndexName);
        connectionSettings.ThrowExceptions();

        // means that we do not change property names when indexing (like pascal case to camel case)
        connectionSettings.DefaultFieldNameInferrer(x => x);

        _client = new ElasticClient(connectionSettings);

        // create an index
        _indexer = new ElasticSearchIndexer(uri, username, password, certificateFingerprint);
        _indexer.CreateOrUpdateIndex(IndexName, projectionDocumentSchema).Wait();
    }

    public string IndexName
    {
        get
        {
            if (string.IsNullOrEmpty(_indexName))
            {
                _indexName = _projectionDocumentSchema.SchemaName;
            }

            return _indexName.ToLower();
        }
    }

    public string? KeyColumnName
    {
        get
        {
            if (string.IsNullOrEmpty(_keyPropertyName))
            {
                _keyPropertyName = _projectionDocumentSchema.KeyColumnName;
            }

            return _keyPropertyName;
        }
    }

    public async Task<Dictionary<string, object?>?> Single(Guid id, string partitionKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var item = await _client.GetAsync<Dictionary<string, object?>>(
                id,
                x => x.Routing(partitionKey),
                ct: cancellationToken
            );

            if (item?.Source == null)
            {
                return null;
            }

            return DeserializeDictionary(item.Source);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to retrieve document with {@Id} ({@Index})", id, IndexName
            );

            throw;
        }
    }

    public async Task Delete(Guid id, string partitionKey, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.DeleteAsync(
                new DeleteRequest(Indices.Index(IndexName), id)
                {
                    Routing = new Routing(partitionKey)
                },
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex, 
                "Failed to delete Document with {@Id} ({@Index})", id, IndexName
            );

            throw;
        }
    }

    public async Task DeleteAll(string? partitionKey = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (partitionKey == null)
            {
                await _client.Indices.DeleteAsync(new DeleteIndexRequest(IndexName), cancellationToken);
            }
            else
            {
                await _client.DeleteByQueryAsync<Dictionary<string, object?>>(
                    x => x.Query(
                        q => q.Bool(
                            b => new BoolQuery
                            {
                                Filter = new List<QueryContainer>
                                {
                                    new QueryStringQuery() { Query = $"{nameof(partitionKey)}:{partitionKey}" }
                                }
                            }
                        )
                    )
                    .Routing(partitionKey),
                    cancellationToken
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete index {@Index}", IndexName);
            throw;
        }
    }

    public async Task Upsert(Dictionary<string, object?> document, string partitionKey, CancellationToken cancellationToken = default)
    {
        try
        {
            document.TryGetValue("Id", out object? id);
            document[nameof(ProjectionDocument.PartitionKey)] = partitionKey;

            await _client.IndexAsync(
                new IndexRequest<Dictionary<string, object?>>(document, id: id?.ToString())
                {
                    Routing = new Routing(partitionKey)
                },
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex, 
                "Failed to upsert Document with {@Id} ({@Index})", document[_projectionDocumentSchema.KeyColumnName], IndexName
            );

            throw;
        }
    }

    public async Task<IReadOnlyCollection<Dictionary<string, object?>>> Query(
        ProjectionQuery projectionQuery,
        string? partitionKey = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var result = await _client.SearchAsync<Dictionary<string, object?>>(request =>
            {
                request = request.TrackTotalHits();
                request = request.Query(q => ConstructSearchQuery(q, projectionQuery));
                request = request.Sort(s => ConstructSort(s, projectionQuery));
                request = request.Skip(projectionQuery.Offset);
                request = request.Take(projectionQuery.Limit);

                if (!string.IsNullOrEmpty(partitionKey))
                {
                    request = request.Routing(partitionKey);
                }

                return request;
            });

            return result.Documents;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error querying index ({@Index})", IndexName
            );

            throw;
        }
    }

    private Dictionary<string, object?> DeserializeDictionary(Dictionary<string, object?> document)
    {
        var newDictionary = new Dictionary<string, object?>(document);

        foreach (var kv in newDictionary)
        {
            var propertySchema = _projectionDocumentSchema.Properties.FirstOrDefault(p => p.PropertyName == kv.Key);

            if (propertySchema == null)
            {
                continue;
            }

            if (kv.Value is JsonElement valueAsJsonElement)
            {
                newDictionary[kv.Key] = JsonToObjectConverter.Convert(valueAsJsonElement, propertySchema);
            }
            else if (propertySchema.PropertyType == TypeCode.Object && kv.Value is string)
            {
                newDictionary[kv.Key] = Guid.Parse(kv.Value as string);
            }
            // ES Nest returns int as long for some reason
            else if (propertySchema.PropertyType == TypeCode.Int32)
            {
                newDictionary[kv.Key] = Convert.ToInt32(kv.Value);
            }
        }

        return newDictionary;
    }

    private QueryContainer ConstructSearchQuery<T>(QueryContainerDescriptor<T> searchDescriptor, ProjectionQuery projectionQuery) where T : class
    {
        // construct search query
        List<QueryContainer> textQueries = new();

        if (!string.IsNullOrWhiteSpace(projectionQuery.SearchText) && projectionQuery.SearchText != "*")
        {
            var queries = ElasticSearchQueryFactory.ConstructSearchQuery(_projectionDocumentSchema, projectionQuery.SearchText);

            textQueries.Add(new BoolQuery()
            {
                Should = queries
            });
        }

        if (projectionQuery.Filters == null || !projectionQuery.Filters.Any())
        {
            return searchDescriptor.Bool(q =>
                new BoolQuery()
                {
                    Should = textQueries
                }
            );
        }

        // construct filters
        var filters = ElasticSearchFilterFactory.ConstructFilters(projectionQuery.Filters);

        return searchDescriptor.Bool(q =>
            new BoolQuery()
            {
                Should = textQueries,
                Filter = filters
            }
        );
    }

    private SortDescriptor<T> ConstructSort<T>(SortDescriptor<T> sortDescriptor, ProjectionQuery projectionQuery) where T : class
    {
        foreach (var orderBy in projectionQuery.OrderBy)
        {
            sortDescriptor = sortDescriptor.Field(
                new Nest.Field(orderBy.Key),
                orderBy.Value.ToLower() == "asc" ? Nest.SortOrder.Ascending : Nest.SortOrder.Descending
            );
        }

        return sortDescriptor;
    }
}
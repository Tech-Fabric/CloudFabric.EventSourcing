using System.Collections;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using CloudFabric.Projections.ElasticSearch.Helpers;
using CloudFabric.Projections.Queries;
using CloudFabric.Projections.Utils;

using Nest;

using SortOrder = Nest.SortOrder;

namespace CloudFabric.Projections.ElasticSearch;

public class ElasticSearchProjectionRepository<TProjectionDocument> : ElasticSearchProjectionRepository, IProjectionRepository<TProjectionDocument>
    where TProjectionDocument : ProjectionDocument
{
    public ElasticSearchProjectionRepository(
        string uri,
        string username,
        string password,
        string certificateFingerprint,
        ILoggerFactory loggerFactory
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

    public Task Upsert(TProjectionDocument document, string partitionKey, DateTime updatedAt, CancellationToken cancellationToken = default)
    {
        var documentDictionary = ProjectionDocumentSerializer.SerializeToDictionary(document);
        return Upsert(documentDictionary, partitionKey, updatedAt, cancellationToken);
    }

    public new async Task<ProjectionQueryResult<TProjectionDocument>> Query(ProjectionQuery projectionQuery, string? partitionKey = null, CancellationToken cancellationToken = default)
    {
        var recordsDictionary = await base.Query(projectionQuery, partitionKey, cancellationToken);

        var records = new List<QueryResultDocument<TProjectionDocument>>();

        foreach (var doc in recordsDictionary.Records)
        {
            records.Add(
                new QueryResultDocument<TProjectionDocument>
                {
                    Document = ProjectionDocumentSerializer.DeserializeFromDictionary<TProjectionDocument>(doc.Document)
                }
            );
        }

        return new ProjectionQueryResult<TProjectionDocument>
        {
            IndexName = recordsDictionary.IndexName,
            TotalRecordsFound = recordsDictionary.TotalRecordsFound,
            Records = records
        };
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
        ILoggerFactory loggerFactory
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
        _indexer = new ElasticSearchIndexer(uri, username, password, certificateFingerprint, loggerFactory);
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

    public async Task EnsureIndex(CancellationToken cancellationToken = default)
    {
        await _indexer.CreateOrUpdateIndex(IndexName, _projectionDocumentSchema);
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

    public async Task Upsert(Dictionary<string, object?> document, string partitionKey, DateTime updatedAt, CancellationToken cancellationToken = default)
    {
        try
        {
            document.TryGetValue("Id", out object? id);
            document[nameof(ProjectionDocument.PartitionKey)] = partitionKey;
            document[nameof(ProjectionDocument.UpdatedAt)] = updatedAt;

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

    public async Task<ProjectionQueryResult<Dictionary<string, object?>>> Query(
        ProjectionQuery projectionQuery,
        string? partitionKey = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var result = await _client.SearchAsync<Dictionary<string, object?>>(
                request =>
                {
                    request = request.TrackTotalHits();
                    request = request.Query(q => ConstructSearchQuery(q, projectionQuery));
                    request = request.Sort(s => ConstructSort(s, projectionQuery));
                    request = request.Skip(projectionQuery.Offset);

                    if (projectionQuery.Limit.HasValue)
                    {
                        request = request.Take(projectionQuery.Limit.Value);
                    }

                    if (!string.IsNullOrEmpty(partitionKey))
                    {
                        request = request.Routing(partitionKey);
                    }

                    return request;
                }
            );

            return new ProjectionQueryResult<Dictionary<string, object?>>
            {
                IndexName = IndexName,
                TotalRecordsFound = (int)result.Total,
                Records = result.Documents.Select(x => 
                    new QueryResultDocument<Dictionary<string, object?>>
                    {
                        Document = DeserializeDictionary(x)
                    }
                ).ToList()
            };
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

            if (propertySchema != null)
            {
                try
                {
                    newDictionary[kv.Key] = DeserializeDictionaryItem(kv.Value, propertySchema);
                }
                catch (Exception ex)
                {
                    throw new Exception(
                        $"Failed to deserialize dictionary item for property {propertySchema.PropertyName}:{propertySchema.PropertyType}, " +
                        $"value: {kv.Value}", ex
                    );
                }
            }
        }

        return newDictionary;
    }

    private object? DeserializeDictionaryItem(object? item, ProjectionDocumentPropertySchema propertySchema)
    {
        if (item == null)
        {
            return null;
        }
        else if (item is JsonElement valueAsJsonElement)
        {
            return JsonToObjectConverter.Convert(valueAsJsonElement, propertySchema);
        }
        else if (propertySchema.PropertyType == TypeCode.Object && item is string)
        {
            return Guid.Parse(item as string);
        }
        // ES Nest returns int as long for some reason
        else if (propertySchema.PropertyType == TypeCode.Int32)
        {
            return Convert.ToInt32(item);
        }
        else if (propertySchema.PropertyType == TypeCode.Decimal)
        {
            return Convert.ToDecimal(item);
        }
        else if (propertySchema.IsNestedObject)
        {
            var nestedObject = item as Dictionary<string, object?>;
            foreach (var nestedProperty in nestedObject)
            {
                var nestedPropertySchema = propertySchema.NestedObjectProperties.First(x => x.PropertyName == nestedProperty.Key);
                nestedObject[nestedProperty.Key] = DeserializeDictionaryItem(nestedProperty.Value, nestedPropertySchema);
            }

            return nestedObject;
        }
        else if (propertySchema.IsNestedArray && propertySchema.ArrayElementType == TypeCode.Object)
        {
            IList<object?> resultList = new List<object?>();
            foreach (var listItem in (item as IList))
            {
                var listItemDictionary = listItem as Dictionary<string, object?>;
                foreach (var listItemProperty in listItemDictionary)
                {
                    listItemDictionary[listItemProperty.Key] = DeserializeDictionaryItem(
                        listItemProperty.Value, 
                        propertySchema.NestedObjectProperties.First(x => x.PropertyName == listItemProperty.Key)
                    );
                } 
                
                resultList.Add(listItemDictionary);
            }

            return resultList;
        }

        return item;
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
            q.Must(
                new BoolQuery
                {
                    Should = textQueries
                },
                new BoolQuery
                {
                    Filter = filters
                }
            )
        );
    }

    private SortDescriptor<T> ConstructSort<T>(SortDescriptor<T> sortDescriptor, ProjectionQuery projectionQuery) where T : class
    {
        foreach (var orderBy in projectionQuery.OrderBy)
        {
            var keyPaths = orderBy.KeyPath.Split('.');
            SortOrder sortOrder = orderBy.Order.ToLower() == "asc" ? Nest.SortOrder.Ascending : Nest.SortOrder.Descending;
            
            sortDescriptor = sortDescriptor.Field(
                field =>
                {
                    var descriptor = field
                        .Field(new Nest.Field(orderBy.KeyPath))
                        .Order(sortOrder);

                    // add sorting statement for each nested level
                    for (var pathElementsNumber = 1; pathElementsNumber < keyPaths.Length; pathElementsNumber++)
                    {
                        descriptor = descriptor
                            .Nested(nested =>
                            {
                                string nestedPath = string.Join('.', keyPaths.Take(pathElementsNumber));
                                
                                var nestedDescriptor = nested.Path(nestedPath);

                                // check property filters (for example if we sort by an array element, we need to filter it)
                                if (orderBy.Filters.Any())
                                {
                                    // take parent path for current nested level
                                    string parentObjectPath = nestedPath.Contains(".")
                                        ? nestedPath.Substring(0, nestedPath.LastIndexOf('.'))
                                        : nestedPath;
                                    
                                    // find a filter with the same parent path
                                    // which means to filter by a property of the same object
                                    var filter = orderBy.Filters.FirstOrDefault(
                                        x =>
                                        {
                                            string filterParentPath = x.FilterKeyPath.Contains(".")
                                                ? x.FilterKeyPath.Substring(0, x.FilterKeyPath.LastIndexOf('.'))
                                                : x.FilterKeyPath;
                                                
                                            return filterParentPath == parentObjectPath;
                                        }
                                    );

                                    if (filter != null)
                                    {
                                        nestedDescriptor.Filter(
                                            f => f
                                                .Term(
                                                    term => term
                                                        .Field(filter.FilterKeyPath)
                                                        .Value(filter.FilterValue)
                                                )
                                        );
                                    }
                                }

                                return nestedDescriptor;
                            });
                    }

                    return descriptor;
                }
            );
        }

        return sortDescriptor;
    }
}

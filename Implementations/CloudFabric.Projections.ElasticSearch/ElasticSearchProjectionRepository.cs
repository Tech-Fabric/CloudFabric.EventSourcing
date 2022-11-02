using System.Linq;
using System.Security.AccessControl;
using System.Text.Json;
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
            if (kv.Value is JsonElement valueAsJsonElement)
            {
                var propertySchema = _projectionDocumentSchema.Properties.FirstOrDefault(p => p.PropertyName == kv.Key);

                if (propertySchema != null)
                {
                    newDictionary[kv.Key] = JsonToObjectConverter.Convert(valueAsJsonElement, propertySchema);
                }
            }
        }

        return newDictionary;
    }

    private QueryContainer ConstructSearchQuery<T>(QueryContainerDescriptor<T> searchDescriptor, ProjectionQuery projectionQuery) where T : class
    {
        // construct search query
        QueryBase textQuery = new MatchAllQuery();

        if (!string.IsNullOrWhiteSpace(projectionQuery.SearchText) && projectionQuery.SearchText != "*")
        {
            var searchableProperties = _projectionDocumentSchema.Properties.Where(x => x.IsSearchable).Select(p => p.PropertyName).ToList();

            textQuery = new NestedQuery()
            {
                Path = "Items",
                Query = new BoolQuery()
                {
                    Filter = new List<QueryContainer>()
                    {
                        new QueryStringQuery() { Query = projectionQuery.SearchText }
                    }
                }
                //new WildcardQuery 
                //{
                //    Field = "Items.Name",
                //    Value = $"*{projectionQuery.SearchText}*"
                //} 
                //Fields = "Items.Name"//string.Join(',', searchableProperties.Select(x => x.PropertyName))
            };
        }

        if (projectionQuery.Filters == null || !projectionQuery.Filters.Any())
        {
            return textQuery;
        }

        // construct filters
        var filterStrings = new List<string>();

        foreach (var f in projectionQuery.Filters)
        {
            var conditionFilter = $"({ConstructConditionFilter(f)})";
            var propName = f.PropertyName == null ? f.Filters[0].Filter.PropertyName : f.PropertyName;

            if (f.PropertyName?.IndexOf(".") == -1)
            {
                filterStrings.Add(conditionFilter);
            }
        }

        var filter = new List<QueryContainer>()
        {
            new QueryStringQuery() { Query = string.Join(" AND ", filterStrings) }
        };

        var nestedQueryStrings = ConstructNestedQueryFilters(projectionQuery.Filters);

        foreach (var entry in nestedQueryStrings)
        {
            var nestedFilter = new NestedQuery()
            {
                Path = entry.Key,
                Query = new BoolQuery()
                {
                    Filter = new List<QueryContainer>()
                    {
                        new QueryStringQuery() { Query = entry.Value }
                    }
                }
            };

            filter.Add(nestedFilter);
        }

        return searchDescriptor.Bool(q =>
            new BoolQuery()
            {
                Must = new List<QueryContainer>() { textQuery },
                Filter = filter
            }
        );
    }

    // in order to mark the nested property as searchable all the chain of properies from parent to child should be marked as searchable
    private string ConstructSearchTerms(string searchText)
    {
        var queries = new List<QueryContainer>();

        // find all searchable fields
        var notNestedSearchableProperties = _projectionDocumentSchema.Properties.Where(x => x.IsSearchable && !x.IsNestedObject && !x.IsNestedArray);
        var nestedSearchableProperties = _projectionDocumentSchema.Properties.Where(x => x.IsSearchable && (x.IsNestedObject || x.IsNestedArray));

        // create basic search queries for 1-level fields
        if (!notNestedSearchableProperties.Any())
        {
            queries.Add(new QueryStringQuery()
            {
                Query = searchText,
                Fields = string.Join(',', notNestedSearchableProperties.Where(x => !x.IsNestedObject && !x.IsNestedArray).Select(x => x.PropertyName))
            });
        }

        // created queries for nested fields
        foreach (var nestedProp in nestedSearchableProperties)
        {

        }

        return null;
    }

    private Dictionary<string, string> ConstructNestedQueryFilters(List<Queries.Filter> filters)
    {
        var result = new Dictionary<string, string>();

        if (filters == null || filters.Count == 0)
        {
            return result;
        }

        var nestedFiltersStrings = new Dictionary<string, List<string>>();

        foreach (var f in filters)
        {
            var propName = f.PropertyName == null ? f.Filters[0].Filter.PropertyName : f.PropertyName;
            var pathParts = propName.Split('.');

            if (pathParts.Count() <= 1)
            {
                continue;
            }

            var conditionFilter = $"({ConstructConditionFilter(f)})";
            var nestedPath = string.Join(".", pathParts.Take(pathParts.Length - 1));

            if (!nestedFiltersStrings.ContainsKey(nestedPath))
            {
                nestedFiltersStrings[nestedPath] = new List<string>();
            }

            nestedFiltersStrings[nestedPath].Add(conditionFilter);
        }

        foreach (var entry in nestedFiltersStrings)
        {
            result[entry.Key] = string.Join(" AND ", entry.Value);
        }

        return result;
    }

    private string ConstructConditionFilter(Queries.Filter filter)
    {
        var q = ConstructOneConditionFilter(filter);

        foreach (FilterConnector f in filter.Filters)
        {
            if (!string.IsNullOrEmpty(q) && f.Logic != null)
            {
                q += $" {f.Logic.ToUpper()} ";
            }

            var wrapWithParentheses = f.Logic != null;

            if (wrapWithParentheses)
            {
                q += "(";
            }

            q += ConstructConditionFilter(f.Filter);

            if (wrapWithParentheses)
            {
                q += ")";
            }
        }

        return q;
    }

    private string ConstructOneConditionFilter(Queries.Filter filter)
    {
        if (string.IsNullOrEmpty(filter.PropertyName))
        {
            return "";
        }

        var filterOperator = "";
        switch (filter.Operator)
        {
            case FilterOperator.NotEqual:
            case FilterOperator.Equal:
                filterOperator = ":";
                break;
            case FilterOperator.Greater:
                filterOperator = ":>";
                break;
            case FilterOperator.GreaterOrEqual:
                filterOperator = ":>=";
                break;
            case FilterOperator.Lower:
                filterOperator = ":<";
                break;
            case FilterOperator.LowerOrEqual:
                filterOperator = ":<=";
                break;
        }

        var filterValue = filter.Value.ToString();

        var condition = $"{filter.PropertyName}{filterOperator}{filterValue}";
        if (filter.Value == null)
        {
            condition = $"({condition} OR (!(_exists_:{filter.PropertyName})))";
        }

        if (filter.Operator == FilterOperator.NotEqual)
        {
            return $"!({condition})";
        }

        return condition;
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
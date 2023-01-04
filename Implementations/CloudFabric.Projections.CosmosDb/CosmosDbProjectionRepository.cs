using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using CloudFabric.Projections.Queries;
using CloudFabric.Projections.Utils;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Polly;

namespace CloudFabric.Projections.CosmosDb;

internal sealed class CosmosDbSqlParameter
{
    public CosmosDbSqlParameter(string name, object? value)
    {
        Name = name;
        Value = value;
    }

    public string Name { get; set; }
    public object? Value { get; set; }
}

public class CosmosDbProjectionRepository<TProjectionDocument>
    : CosmosDbProjectionRepository, IProjectionRepository<TProjectionDocument>
    where TProjectionDocument : ProjectionDocument
{
    public CosmosDbProjectionRepository(
        ILoggerFactory loggerFactory,
        string connectionString,
        CosmosClientOptions cosmosClientOptions,
        string databaseId,
        string containerId
    ) : base(
        loggerFactory,
        connectionString,
        cosmosClientOptions,
        databaseId,
        containerId,
        ProjectionDocumentSchemaFactory.FromTypeWithAttributes<TProjectionDocument>()
    )
    {
    }

    public new async Task<TProjectionDocument?> Single(Guid id, string partitionKey, CancellationToken cancellationToken = default)
    {
        var document = await base.Single(id, partitionKey, cancellationToken);

        if (document == null) return null;

        return ProjectionDocumentSerializer.DeserializeFromDictionary<TProjectionDocument>(document);
    }

    public Task Upsert(TProjectionDocument document, string partitionKey, DateTime updatedAt, CancellationToken cancellationToken = default)
    {
        var documentDictionary = ProjectionDocumentSerializer.SerializeToDictionary(document);
        return Upsert(documentDictionary, partitionKey, updatedAt, cancellationToken);
    }

    public new async Task<ProjectionQueryResult<TProjectionDocument>> Query(
        ProjectionQuery projectionQuery,
        string? partitionKey = null,
        CancellationToken cancellationToken = default
    )
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

public class CosmosDbProjectionRepository : IProjectionRepository
{
    private readonly CosmosClient _client;
    private readonly string _containerId;
    private readonly string _databaseId;
    private readonly ILogger<CosmosDbProjectionRepository> _logger;

    private readonly ProjectionDocumentSchema _projectionDocumentSchema;

    private Dictionary<string, string> _propertyNamesMapping = new Dictionary<string, string>()
    {
        { "Id", "id" },
        { "PartitionKey", "partitionKey" }
    };

    public CosmosDbProjectionRepository(
        ILoggerFactory loggerFactory,
        string connectionString,
        CosmosClientOptions cosmosClientOptions,
        string databaseId,
        string containerId,
        ProjectionDocumentSchema projectionDocumentSchema
    )
    {
        _logger = loggerFactory.CreateLogger<CosmosDbProjectionRepository>();
        _client = new CosmosClient(connectionString, cosmosClientOptions);
        _databaseId = databaseId;
        _containerId = containerId;
        _projectionDocumentSchema = projectionDocumentSchema;
    }

    public CosmosDbProjectionRepository(
        ILoggerFactory loggerFactory,
        CosmosClient client,
        string databaseId,
        string containerId
    )
    {
        _logger = loggerFactory.CreateLogger<CosmosDbProjectionRepository>();
        _client = client;
        _databaseId = databaseId;
        _containerId = containerId;
    }

    public async Task<Dictionary<string, object?>?> Single(Guid id, string partitionKey, CancellationToken cancellationToken = default)
    {
        Container container = _client.GetContainer(_databaseId, _containerId);
        var sw = Stopwatch.StartNew();

        try
        {
            var result = await ExecuteWithRetries(
                c => container.ReadItemAsync<Dictionary<string, object?>>(
                    id.ToString(),
                    new PartitionKey(partitionKey),
                    cancellationToken: c
                ),
                cancellationToken
            );

            return DeserializeDictionary(result);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex, "Failed to retrieve {@Id} ({@Partition}/{@Container})", id, partitionKey,
                container.Id
            );
            throw;
        }
        finally
        {
            _logger.LogDebug(
                "CosmosDB Get Document with {@Id} ({@Partition}/{@Container}) executed in {@ExecutionTime} ms",
                id, partitionKey, container.Id, sw.ElapsedMilliseconds
            );
        }
    }

    public async Task Delete(Guid id, string partitionKey, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        Container container = _client.GetContainer(_databaseId, _containerId);

        try
        {
            await ExecuteWithRetries(
                c => container.DeleteItemAsync<dynamic>(id.ToString(), new PartitionKey(partitionKey), cancellationToken: c),
                cancellationToken
            );
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // NOP: document doesn't exist
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex, "Error deleting Document with {@Id} ({@Partition}/{@Container})", id, partitionKey,
                container.Id
            );
            throw;
        }
        finally
        {
            _logger.LogDebug(
                "CosmosDB Delete Document with {@Id} ({@Partition}/{@Container}) executed in {@ExecutionTime} ms", id,
                partitionKey, container.Id, sw.ElapsedMilliseconds
            );
        }
    }

    public async Task DeleteAll(string? partitionKey = null, CancellationToken cancellationToken = default)
    {
        Container container = _client.GetContainer(_databaseId, _containerId);

        if (string.IsNullOrEmpty(partitionKey))
        {
            await container.DeleteContainerAsync(null, cancellationToken);
        }
        else
        {
            throw new NotImplementedException("Bulk delete is not supported for now");
        }
    }

    public async Task Upsert(Dictionary<string, object?> document, string partitionKey, DateTime updatedAt, CancellationToken cancellationToken = default)
    {
        if (document[_projectionDocumentSchema.KeyColumnName] == null)
        {
            throw new ArgumentException("document primary key cannot be null", _projectionDocumentSchema.KeyColumnName);
        }

        document.TryGetValue(nameof(ProjectionDocument.Id), out object? id);
        document[nameof(ProjectionDocument.PartitionKey)] = partitionKey;
        document[nameof(ProjectionDocument.UpdatedAt)] = updatedAt;

        var sw = Stopwatch.StartNew();

        Container container = _client.GetContainer(_databaseId, _containerId);

        try
        {
            var json = JsonSerializer.SerializeToDocument(SerializeDictionary(document));
            await ExecuteWithRetries(
                c => container.UpsertItemAsync(json, new PartitionKey(partitionKey), cancellationToken: c),
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex, "Failed to upsert Document with {@Id} ({@Partition}/{@Container}) is not found",
                document[_projectionDocumentSchema.KeyColumnName], partitionKey, container.Id
            );
            throw;
        }
        finally
        {
            _logger.LogDebug(
                "CosmosDB Upsert Document with {@Id} ({@Partition}/{@Container}) executed in {@ExecutionTime} ms",
                document[_projectionDocumentSchema.KeyColumnName], partitionKey, container.Id, sw.ElapsedMilliseconds
            );
        }
    }

    public async Task<ProjectionQueryResult<Dictionary<string, object?>>> Query(
        ProjectionQuery projectionQuery,
        string? partitionKey = null,
        CancellationToken cancellationToken = default
    )
    {
        var properties = _projectionDocumentSchema.Properties;

        var sb = new StringBuilder();
        sb.Append("SELECT ");
        sb.AppendJoin(',', properties.Select(p => $"t.{SerializePropertyName(p.PropertyName)}"));
        sb.Append(" FROM ");
        sb.Append(_containerId);
        sb.Append(" t");
        sb.Append(" WHERE ");

        var (whereClause, parameters) = ConstructConditionFilters(projectionQuery.Filters);
        
        if (!string.IsNullOrWhiteSpace(projectionQuery.SearchText) && projectionQuery.SearchText != "*")
        {
            (string searchQuery, CosmosDbSqlParameter param) = ConstructSearchQuery(projectionQuery.SearchText);
            whereClause = string.IsNullOrWhiteSpace(whereClause) ? $" {searchQuery}" : $"{whereClause} AND {searchQuery}";
            parameters.Add(param);
        }

        // total count query and params
        string totalCountQuery = $"SELECT COUNT(1) FROM {_containerId} t WHERE {whereClause}";
        CosmosDbSqlParameter[] totalCountParams = new CosmosDbSqlParameter[parameters.Count];
        parameters.CopyTo(totalCountParams);

        sb.Append(whereClause);
        sb.Append(" OFFSET @offset");
        parameters.Add(new CosmosDbSqlParameter("offset", projectionQuery.Offset));
        sb.Append(" LIMIT @limit");
        parameters.Add(new CosmosDbSqlParameter("limit", projectionQuery.Limit));

        if (projectionQuery.OrderBy.Count > 0)
        {
            // NOTE: nested sorting is not implemented
            sb.Append(" ORDER BY ");
            sb.AppendJoin(',', projectionQuery.OrderBy.Select(kv => $"{kv.KeyPath} {kv.Order}"));
        }

        var queryDefinition = new QueryDefinition(sb.ToString());

        foreach (var param in parameters)
        {
            queryDefinition = queryDefinition.WithParameter($"@{param.Name}", param.Value);
        }

        var results = new List<Dictionary<string, object?>>();

        var sw = Stopwatch.StartNew();

        Container container = _client.GetContainer(_databaseId, _containerId);

        try
        {
            var requestOptions = GetQueryOptions(partitionKey);

            var iterator = container.GetItemQueryIterator<Dictionary<string, object?>>(queryDefinition, null, requestOptions);

            while (iterator.HasMoreResults && results.Count < projectionQuery.Limit)
            {
                var batchResults = await ExecuteWithRetries(c => iterator.ReadNextAsync(c), cancellationToken);
                results.AddRange(batchResults.Select(DeserializeDictionary));
            }
            
            // get total count
            queryDefinition = new QueryDefinition(totalCountQuery);

            foreach (var param in totalCountParams)
            {
                queryDefinition = queryDefinition.WithParameter($"@{param.Name}", param.Value);
            }
            
            FeedIterator<int> totalCountIterator = container.GetItemQueryIterator<int>(queryDefinition, null, requestOptions);
            var totalCountResponse = await totalCountIterator.ReadNextAsync();

            return new ProjectionQueryResult<Dictionary<string, object?>>
            {
                IndexName = _containerId,
                TotalRecordsFound = totalCountResponse.Resource.FirstOrDefault(),
                Records = results.Select(x => 
                    new QueryResultDocument<Dictionary<string, object?>>
                    {
                        Document = x
                    }
                ).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying container {@Container}", container.Id);
            throw;
        }
        finally
        {
            _logger.LogDebug(
                "CosmosDB query for container {@Container} executed in {@ExecutionTime} ms", container.Id,
                sw.ElapsedMilliseconds
            );
        }
    }

    private (string, CosmosDbSqlParameter?) ConstructOneConditionFilter(Filter filter)
    {
        var filterOperator = "";
        var propertyName = filter.PropertyName;

        if (string.IsNullOrEmpty(propertyName))
        {
            return (string.Empty, null);
        }

        switch (filter.Operator)
        {
            case FilterOperator.Equal:
                filterOperator = "=";
                break;
            case FilterOperator.Greater:
                filterOperator = ">";
                break;
            case FilterOperator.GreaterOrEqual:
                filterOperator = ">=";
                break;
            case FilterOperator.Lower:
                filterOperator = "<";
                break;
            case FilterOperator.LowerOrEqual:
                filterOperator = "<=";
                break;
            case FilterOperator.NotEqual:
                filterOperator = "!=";
                break;
        }

        return ($"t.{propertyName} {filterOperator} @{propertyName}", new CosmosDbSqlParameter(propertyName, filter.Value));
    }

    private (string, List<CosmosDbSqlParameter>) ConstructConditionFilter(Filter filter)
    {
        var parameters = new List<CosmosDbSqlParameter>();

        var (q, param) = ConstructOneConditionFilter(filter);

        if (param != null)
        {
            parameters.Add(param);
        }

        foreach (FilterConnector f in filter.Filters)
        {
            if (!string.IsNullOrEmpty(q))
            {
                q += $" {f.Logic} ";
            }

            var wrapWithParentheses = f.Filter.Filters.Count > 0;

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

        return (q, parameters);
    }

    private (string, List<CosmosDbSqlParameter>) ConstructConditionFilters(List<Filter> filters)
    {
        var allParameters = new List<CosmosDbSqlParameter>();
        var whereClauses = new List<string>();
        foreach (var f in filters)
        {
            var (whereClause, parameters) = ConstructConditionFilter(f);
            whereClauses.Add(whereClause);
            allParameters.AddRange(parameters);
        }

        return (string.Join(" AND ", whereClauses), allParameters);
    }

    private (string, CosmosDbSqlParameter) ConstructSearchQuery(string searchText)
    {
        var searchableProperties = _projectionDocumentSchema.Properties.Where(x => x.IsSearchable);

        List<string> query = new List<string>();

        foreach (var property in searchableProperties)
        {
            query.Add($"LOWER(t.{SerializePropertyName(property.PropertyName)}) LIKE LOWER(@searchText)");
        }

        return (
            $"({string.Join(" OR ", query)})",
            new CosmosDbSqlParameter("searchText", $"%{searchText}%")
        );
    }

    private Task<T> ExecuteWithRetries<T>(
        Func<CancellationToken, Task<T>> function,
        CancellationToken cancellationToken
    ) =>
        Policy
            .Handle<CosmosException>(
                exception =>
                    exception.StatusCode == HttpStatusCode.TooManyRequests ||
                    exception.StatusCode == HttpStatusCode.RequestTimeout
            )
            .RetryForeverAsync(
                exception =>
                {
                    var cosmosException = (CosmosException)exception;

                    var retryAfter = cosmosException.RetryAfter ?? TimeSpan.FromMilliseconds(100);

                    _logger.LogDebug(
                        "{reason} - sleeping for {delay}",
                        cosmosException.StatusCode == HttpStatusCode.RequestTimeout ? "Timeout" : "Rate limitting",
                        retryAfter
                    );

                    return Task.Delay(retryAfter, cancellationToken);
                }
            )
            .ExecuteAsync(function, cancellationToken);


    private static QueryRequestOptions? GetQueryOptions(string partitionKey)
    {
        return !string.IsNullOrWhiteSpace(partitionKey)
            ? new QueryRequestOptions { PartitionKey = new PartitionKey(partitionKey) }
            : null;
    }

    private string SerializePropertyName(string propertyName)
    {
        if (_propertyNamesMapping.ContainsKey(propertyName))
        {
            return _propertyNamesMapping[propertyName];
        }

        return propertyName;
    }

    private Dictionary<string, object?> SerializeDictionary(Dictionary<string, object?> document)
    {
        var newDictionary = new Dictionary<string, object?>(document);

        foreach (var propertyName in _propertyNamesMapping)
        {
            if (newDictionary.ContainsKey(propertyName.Key))
            {
                newDictionary[propertyName.Value] = newDictionary[propertyName.Key];
                newDictionary.Remove(propertyName.Key);
            }
        }

        return newDictionary;
    }

    private Dictionary<string, object?> DeserializeDictionary(Dictionary<string, object?> document)
    {
        var newDictionary = new Dictionary<string, object?>(document);

        foreach (var propertyName in _propertyNamesMapping)
        {
            if (newDictionary.ContainsKey(propertyName.Value))
            {
                newDictionary[propertyName.Key] = newDictionary[propertyName.Value];
                newDictionary.Remove(propertyName.Value);
            }
        }

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
}

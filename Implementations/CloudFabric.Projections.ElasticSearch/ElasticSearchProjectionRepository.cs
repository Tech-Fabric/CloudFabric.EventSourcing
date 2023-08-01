using System.Collections;
using System.Text.Json;
using CloudFabric.Projections.ElasticSearch.Helpers;
using CloudFabric.Projections.Queries;
using CloudFabric.Projections.Utils;
using Elasticsearch.Net;
using Microsoft.Extensions.Logging;
using Nest;
using SortOrder = Nest.SortOrder;

namespace CloudFabric.Projections.ElasticSearch;

public class ElasticSearchProjectionRepository<TProjectionDocument> : ElasticSearchProjectionRepository, IProjectionRepository<TProjectionDocument>
    where TProjectionDocument : ProjectionDocument
{
    public ElasticSearchProjectionRepository(
        ElasticSearchBasicAuthConnectionSettings basicAuthConnectionSettings,
        ILoggerFactory loggerFactory,
        bool disableRequestStreaming
    ) : base(
        basicAuthConnectionSettings,
        ProjectionDocumentSchemaFactory.FromTypeWithAttributes<TProjectionDocument>(),
        loggerFactory,
        disableRequestStreaming
    )
    {
    }

    public ElasticSearchProjectionRepository(
        ElasticSearchApiKeyAuthConnectionSettings apiKeyAuthConnectionSettings,
        ILoggerFactory loggerFactory,
        bool disableRequestStreaming
    ) : base(
        apiKeyAuthConnectionSettings,
        ProjectionDocumentSchemaFactory.FromTypeWithAttributes<TProjectionDocument>(),
        loggerFactory,
        disableRequestStreaming
    )
    {
    }

    public new async Task<TProjectionDocument?> Single(
        Guid id,
        string partitionKey,
        CancellationToken cancellationToken,
        ProjectionOperationIndexSelector indexSelector = ProjectionOperationIndexSelector.ReadOnly
    )
    {
        var document = await base.Single(id, partitionKey, cancellationToken, indexSelector);

        if (document == null)
        {
            return null;
        }

        return ProjectionDocumentSerializer.DeserializeFromDictionary<TProjectionDocument>(document);
    }

    public Task Upsert(
        TProjectionDocument document,
        string partitionKey,
        DateTime updatedAt,
        CancellationToken cancellationToken = default,
        ProjectionOperationIndexSelector indexSelector = ProjectionOperationIndexSelector.Write
    )
    {
        var documentDictionary = ProjectionDocumentSerializer.SerializeToDictionary(document);
        return Upsert(documentDictionary, partitionKey, updatedAt, cancellationToken, indexSelector);
    }

    public new async Task<ProjectionQueryResult<TProjectionDocument>> Query(
        ProjectionQuery projectionQuery,
        string? partitionKey = null,
        CancellationToken cancellationToken = default,
        ProjectionOperationIndexSelector indexSelector = ProjectionOperationIndexSelector.ReadOnly
    )
    {
        var result = await base.Query(projectionQuery, partitionKey, cancellationToken, indexSelector);

        var records = new List<QueryResultDocument<TProjectionDocument>>();

        foreach (var doc in result.Records)
        {
            if (doc.Document != null)
            {
                records.Add(
                    new QueryResultDocument<TProjectionDocument>
                    {
                        Document = ProjectionDocumentSerializer.DeserializeFromDictionary<TProjectionDocument>(doc.Document)
                    }
                );
            }
        }

        return new ProjectionQueryResult<TProjectionDocument>
        {
            IndexName = result.IndexName,
            TotalRecordsFound = result.TotalRecordsFound,
            Records = records,
            DebugInformation = result.DebugInformation
        };
    }
}

public class ElasticSearchProjectionRepository : ProjectionRepository
{
    private const string PROJECTION_INDEX_STATE_INDEX_NAME = "projection_index_state";

    private readonly ProjectionDocumentSchema _projectionDocumentSchema;
    private string? _keyPropertyName;
    private readonly ElasticClient _client;
    private readonly ElasticSearchIndexer _indexer;
    private readonly ILogger<ElasticSearchProjectionRepository> _logger;

    /// <summary>
    /// When request streaming is disabled, elastic adds debug information about request and response to response object which can
    /// be useful when troubleshooting search problems.
    /// </summary>
    private readonly bool _disableRequestStreaming;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="apiKeyAuthConnectionSettings"></param>
    /// <param name="projectionDocumentSchema"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="disableRequestStreaming">
    /// When request streaming is disabled, elastic adds debug information about request and response to response object which can
    /// be useful when troubleshooting search problems.
    ///
    /// Defaults to false to improve performance.
    /// </param>
    public ElasticSearchProjectionRepository(
        ElasticSearchApiKeyAuthConnectionSettings apiKeyAuthConnectionSettings,
        ProjectionDocumentSchema projectionDocumentSchema,
        ILoggerFactory loggerFactory,
        bool disableRequestStreaming = false
    ) : base(projectionDocumentSchema, loggerFactory.CreateLogger<ProjectionRepository>())
    {
        _projectionDocumentSchema = projectionDocumentSchema;
        _logger = loggerFactory.CreateLogger<ElasticSearchProjectionRepository>();
        _disableRequestStreaming = disableRequestStreaming;

        var connectionSettings = new ConnectionSettings(
                apiKeyAuthConnectionSettings.CloudId, new ApiKeyAuthenticationCredentials(
                    apiKeyAuthConnectionSettings.ApiKeyId,
                    apiKeyAuthConnectionSettings.ApiKey
                )
            )
            .ThrowExceptions()
            .DefaultFieldNameInferrer(x => x);

        _client = new ElasticClient(connectionSettings);
        
        // Very important setting - when we remove the index, system has to create it explicitly, with all custom analyzers and settings.
        // Otherwise the index won't have proper attributes and just won't work
        _client.Cluster.PutSettingsAsync(settings => settings.Persistent(p => { 
            p["action.auto_create_index"] = "false";
            return p;
        }));

        _indexer = new ElasticSearchIndexer(apiKeyAuthConnectionSettings, loggerFactory);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="basicAuthConnectionSettings"></param>
    /// <param name="projectionDocumentSchema"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="disableRequestStreaming">
    /// When request streaming is disabled, elastic adds debug information about request and response to response object which can
    /// be useful when troubleshooting search problems.
    ///
    /// Defaults to false to improve performance.
    /// </param>
    public ElasticSearchProjectionRepository(
        ElasticSearchBasicAuthConnectionSettings basicAuthConnectionSettings,
        ProjectionDocumentSchema projectionDocumentSchema,
        ILoggerFactory loggerFactory,
        bool disableRequestStreaming = false
    ) : base(projectionDocumentSchema, loggerFactory.CreateLogger<ProjectionRepository>())
    {
        _projectionDocumentSchema = projectionDocumentSchema;
        _logger = loggerFactory.CreateLogger<ElasticSearchProjectionRepository>();
        _disableRequestStreaming = disableRequestStreaming;

        var connectionSettings = new ConnectionSettings(new Uri(basicAuthConnectionSettings.Uri))
            .BasicAuthentication(basicAuthConnectionSettings.Username, basicAuthConnectionSettings.Password)
            .CertificateFingerprint(basicAuthConnectionSettings.CertificateThumbprint)
            .ThrowExceptions()
            // means that we do not change property names when indexing (like pascal case to camel case)
            .DefaultFieldNameInferrer(x => x);

        _client = new ElasticClient(connectionSettings);
        
        // Very important setting - when we remove the index, system has to create it explicitly, with all custom analyzers and settings.
        // Otherwise the index won't have proper attributes and just won't work
        _client.Cluster.PutSettingsAsync(settings => settings.Persistent(p => { 
            p["action.auto_create_index"] = "false";
            return p;
        }));

        // create an index
        _indexer = new ElasticSearchIndexer(basicAuthConnectionSettings, loggerFactory);
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
    //
    // /// <summary>
    // /// Internal method for getting index name to work with.
    // /// When projection schema changes, there can be two indexes - one for old version of schema which should still receive updates and queries
    // /// and a new one which should be populated in the background.
    // /// This method checks for available indexes and selects the active one. Once a new index is completed projections rebuild process, this method
    // /// will immediately return a new one, so all updates will go to the new index. 
    // /// </summary>
    // /// <returns></returns>
    // private async Task<string?> GetIndexNameForSchema(bool readOnly)
    // {
    //     var partitionIndexStateIndexName = "partition_index_state";
    //     
    //     var indexStateResponse = await _client.GetAsync<ProjectionIndexState>(
    //         _projectionDocumentSchema.SchemaName,
    //         x => x.Routing(partitionIndexStateIndexName).Index(partitionIndexStateIndexName)
    //     );
    //
    //     if (indexStateResponse.Found)
    //     {
    //         // At least some projection state exists - find the most recent index with completed projections rebuild.
    //         var lastIndexWithRebuiltProjections = indexStateResponse.Source.IndexesStatuses
    //             .Where(i => i.RebuildCompletedAt != null).MaxBy(i => i.RebuildCompletedAt);
    //
    //         if (lastIndexWithRebuiltProjections != null)
    //         {
    //             return lastIndexWithRebuiltProjections.IndexName;
    //         }
    //         
    //         // At least some projection state exists but there is no index which was completely rebuilt. 
    //         // In such situation we could only allow reading from this index, because writing to it may break projections
    //         // events order consistency - if projections rebuild is still in progress we will write an event which happened now before it's preceding
    //         // events not yet processed by projections rebuild process.
    //         if (readOnly)
    //         {
    //             // if there are multiple indexes, we want one that has already started rebuild process.
    //             var lastIndexWithRebuildStarted = indexStateResponse.Source.IndexesStatuses
    //                 .Where(i => i.RebuildStartedAt != null).MaxBy(i => i.RebuildStartedAt);
    //
    //             if (lastIndexWithRebuildStarted != null)
    //             {
    //                 return lastIndexWithRebuildStarted.IndexName;
    //             }
    //             
    //             // If there are multiple indexes but none of them started rebuilding, just return the most recently created one.
    //             var lastIndex = indexStateResponse.Source.IndexesStatuses
    //                 .MaxBy(i => i.CreatedAt);
    //
    //             if (lastIndex != null)
    //             {
    //                 return lastIndex.IndexName;
    //             }
    //         }
    //
    //         throw new IndexNotReadyException(indexStateResponse.Source);
    //     }
    //     else
    //     {
    //         // no index state exists, meaning there is no index at all. 
    //         // Create an empty index state, index background processor is designed to look for records which 
    //         // were created but not populated, it will start the process of projections rebuild once it finds this new record.
    //
    //         var projectionVersionPropertiesHash = ProjectionDocumentSchemaFactory.GetPropertiesUniqueHash(_projectionDocumentSchema.Properties);
    //         var projectionVersionIndexName = $"{_projectionDocumentSchema.SchemaName}_{projectionVersionPropertiesHash}"
    //             .ToLower(); // Elastic throws error saying that index must be lowercase
    //         
    //         var projectionIndexState = new ProjectionIndexState()
    //         {
    //             ProjectionName = _projectionDocumentSchema.SchemaName,
    //             IndexesStatuses = new List<IndexStateForSchemaVersion>() {
    //                 new IndexStateForSchemaVersion()
    //                 {
    //                     CreatedAt = DateTime.UtcNow,
    //                     SchemaHash = projectionVersionPropertiesHash,
    //                     IndexName = projectionVersionIndexName,
    //                     RebuildEventsProcessed = 0,
    //                     RebuildStartedAt = null
    //                 }
    //             }
    //         };
    //         
    //         await _indexer.CreateOrUpdateIndex(projectionVersionIndexName, _projectionDocumentSchema);
    //         
    //         var saveResult = await _client.IndexAsync(
    //             new IndexRequest<ProjectionIndexState>(projectionIndexState, partitionIndexStateIndexName, id: projectionIndexState.ProjectionName)
    //             {
    //                 Routing = new Routing(partitionIndexStateIndexName),
    //                 Refresh = Refresh.True
    //             }
    //         );
    //
    //         return projectionVersionIndexName;
    //     }
    // }

    // public override async Task EnsureIndex(CancellationToken cancellationToken = default)
    // {
    //     _logger.LogInformation("Ensuring index exists for {ProjectionDocumentSchemaName}", _projectionDocumentSchema.SchemaName);
    //
    //     // we just need to make sure index exists `readOnly` will do that, otherwise it will throw an error saying that index is not ready yet
    //     var indexName = await GetIndexNameForSchema(ProjectionOperationIndexSelector.ReadOnly, cancellationToken);
    //
    //     _logger.LogInformation("Index for {ProjectionDocumentSchemaName}, {IndexName}", _projectionDocumentSchema.SchemaName, indexName);
    // }

    protected override async Task CreateIndex(string indexName, ProjectionDocumentSchema projectionDocumentSchema)
    {
        await _indexer.CreateOrUpdateIndex(indexName, _projectionDocumentSchema);
    }

    protected override async Task<ProjectionIndexState?> GetProjectionIndexState(
        CancellationToken cancellationToken = default
    ) {
        return await GetProjectionIndexState(_projectionDocumentSchema.SchemaName, cancellationToken);
    }
    
    protected override async Task<ProjectionIndexState?> GetProjectionIndexState(
        string schemaName,
        CancellationToken cancellationToken = default
    ) {
        var indexStateResponse = await _client.GetAsync<ProjectionIndexState>(
            schemaName,
            x => x.Routing(PROJECTION_INDEX_STATE_INDEX_NAME).Index(PROJECTION_INDEX_STATE_INDEX_NAME)
        );

        return indexStateResponse?.Source;
    }

    protected override async Task SaveProjectionIndexState(ProjectionIndexState state)
    {
        await _indexer.CreateOrUpdateIndex(PROJECTION_INDEX_STATE_INDEX_NAME, ProjectionDocumentSchemaFactory.FromTypeWithAttributes<ProjectionIndexState>());

        var saveResult = await _client.IndexAsync(
            new IndexRequest<ProjectionIndexState>(state, PROJECTION_INDEX_STATE_INDEX_NAME, id: state.ProjectionName)
            {
                Routing = new Routing(PROJECTION_INDEX_STATE_INDEX_NAME),
                Refresh = Refresh.True
            }
        );
    }
    //
    // public override async Task<(ProjectionIndexState?, string?)> AcquireAndLockProjectionThatRequiresRebuild()
    // {
    //     await _indexer.CreateOrUpdateIndex(PROJECTION_INDEX_STATE_INDEX_NAME, ProjectionDocumentSchemaFactory.FromTypeWithAttributes<ProjectionIndexState>());
    //
    //     var rebuildHealthCheckThreshold = DateTime.UtcNow.AddMinutes(-5);
    //
    //     var projectionQuery = new ProjectionQuery()
    //     {
    //         Filters = new List<Filter>()
    //         {
    //             // we are looking for two possible index states:
    //             // 1. RebuildStartedAt = null - index was just created and requires rebuild and
    //             // 2. RebuildCompletedAt = null && RebuildHealthCheckAt < rebuildHealthCheckThreshold - rebuild has started but not completed yet and there
    //             //    have been no health checks for more than 5 minutes (see rebuildHealthCheckThreshold above). Note that this means we are taking over
    //             //    a rebuild not completed by another process. That process could still wake up and continue, so it should check the index before continuing
    //             //    and ensure no other process took over the rebuild process.
    //             new Filter(
    //                 $"{nameof(ProjectionIndexState.IndexesStatuses)}.{nameof(IndexStateForSchemaVersion.RebuildStartedAt)}",
    //                 FilterOperator.Equal,
    //                 null
    //             ).Or(
    //                 new Filter(
    //                         $"{nameof(ProjectionIndexState.IndexesStatuses)}.{nameof(IndexStateForSchemaVersion.RebuildCompletedAt)}", FilterOperator.Equal,
    //                         null
    //                     )
    //                     .And(
    //                         $"{nameof(ProjectionIndexState.IndexesStatuses)}.{nameof(IndexStateForSchemaVersion.RebuildHealthCheckAt)}",
    //                         FilterOperator.Lower,
    //                         rebuildHealthCheckThreshold
    //                     )
    //             )
    //         }
    //     };
    //
    //     var result = await _client.SearchAsync<ProjectionIndexState>(
    //         request =>
    //         {
    //             request = request.Index(PROJECTION_INDEX_STATE_INDEX_NAME);
    //
    //             request = request.TrackTotalHits(false);
    //             request = request.Query(q => ConstructSearchQuery(q, projectionQuery));
    //             request = request.Sort(s => s.Ascending(f => f.UpdatedAt));
    //             request = request.Skip(0);
    //
    //             if (projectionQuery.Limit.HasValue)
    //             {
    //                 request = request.Take(projectionQuery.Limit.Value);
    //             }
    //
    //             if (!string.IsNullOrEmpty(PROJECTION_INDEX_STATE_INDEX_NAME))
    //             {
    //                 request = request.Routing(PROJECTION_INDEX_STATE_INDEX_NAME);
    //             }
    //
    //             if (_disableRequestStreaming)
    //             {
    //                 request = request.RequestConfiguration(conf => conf.DisableDirectStreaming());
    //             }
    //
    //             return request;
    //         }
    //     );
    //
    //     if (result.Documents.Count <= 0)
    //     {
    //         return (null, null);
    //     }
    //
    //     var dateTimeStarted = DateTime.UtcNow;
    //
    //     var projectionIndexState = result.Documents.First();
    //
    //     projectionIndexState.UpdatedAt = dateTimeStarted;
    //
    //     // !Important: this where condition should be in absolute sync with the condition we send to elasticsearch (at the beginning of this method)
    //     var index = projectionIndexState.IndexesStatuses
    //         .Where(s => s.RebuildStartedAt == null || (s.RebuildCompletedAt == null && s.RebuildHealthCheckAt < rebuildHealthCheckThreshold))
    //         .OrderBy(s => s.CreatedAt)
    //         .First();
    //
    //     index.RebuildStartedAt = dateTimeStarted;
    //     index.RebuildHealthCheckAt = dateTimeStarted;
    //     index.RebuildCompletedAt = null;
    //
    //     var saveResult = await _client.IndexAsync(
    //         new IndexRequest<ProjectionIndexState>(projectionIndexState, PROJECTION_INDEX_STATE_INDEX_NAME, id: projectionIndexState.ProjectionName)
    //         {
    //             Routing = new Routing(PROJECTION_INDEX_STATE_INDEX_NAME),
    //             Refresh = Refresh.True
    //         }
    //     );
    //
    //     // we want to make sure no other process locked this item, the easiest way is to just check 
    //     // if saved result has exact same updatedAt timestamp and we were saving within this process 
    //     var indexStateResponse = await _client.GetAsync<ProjectionIndexState>(
    //         projectionIndexState.ProjectionName,
    //         x => x.Routing(PROJECTION_INDEX_STATE_INDEX_NAME).Index(PROJECTION_INDEX_STATE_INDEX_NAME)
    //     );
    //
    //     if (projectionIndexState.UpdatedAt != indexStateResponse.Source.UpdatedAt)
    //     {
    //         // look like some other process updated the item before us. Just ignore this record then - it will be processed by that process.
    //         return (null, null);
    //     }
    //
    //     return (indexStateResponse.Source, index.IndexName);
    // }

    public override async Task UpdateProjectionRebuildStats(ProjectionIndexState indexToRebuild)
    {
        await SaveProjectionIndexState(indexToRebuild);
    }

    public override async Task<Dictionary<string, object?>?> Single(
        Guid id,
        string partitionKey,
        CancellationToken cancellationToken = default,
        ProjectionOperationIndexSelector indexSelector = ProjectionOperationIndexSelector.ReadOnly
    )
    {
        var indexDescriptor = await GetIndexDescriptorForOperation(indexSelector, cancellationToken);

        try
        {
            var item = await _client.GetAsync<Dictionary<string, object?>>(
                id,
                x => x.Index(indexDescriptor.IndexName).Routing(partitionKey),
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
                "Failed to retrieve document with {@Id} ({@Index})", id, indexDescriptor
            );

            throw;
        }
    }

    public override async Task Delete(
        Guid id,
        string partitionKey,
        CancellationToken cancellationToken = default,
        ProjectionOperationIndexSelector indexSelector = ProjectionOperationIndexSelector.Write
    )
    {
        var indexDescriptor = await GetIndexDescriptorForOperation(indexSelector, cancellationToken);

        try
        {
            await _client.DeleteAsync(
                new DeleteRequest(indexDescriptor.IndexName, id)
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
                "Failed to delete Document with {@Id} ({@Index})", id, indexDescriptor
            );

            throw;
        }
    }

    public override async Task DeleteAll(
        string? partitionKey = null,
        CancellationToken cancellationToken = default,
        ProjectionOperationIndexSelector indexSelector = ProjectionOperationIndexSelector.Write
    ) {
        var indexState = await GetProjectionIndexState(cancellationToken);

        if (indexState == null)
        {
            return;
        }
        
        foreach (var indexStatus in indexState.IndexesStatuses)
        {
            try
            {
                if (partitionKey == null)
                {
                    await _client.Indices.DeleteAsync(new DeleteIndexRequest(indexStatus.IndexName), cancellationToken);
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
                _logger.LogError(ex, "Failed to delete index {@Index}", indexStatus.IndexName);
                throw;
            }
        }
        
        await _client.Indices.DeleteAsync(new DeleteIndexRequest(PROJECTION_INDEX_STATE_INDEX_NAME), cancellationToken);
    }

    protected override async Task UpsertInternal(
        ProjectionOperationIndexDescriptor indexDescriptor,
        Dictionary<string, object?> document,
        string partitionKey,
        DateTime updatedAt,
        CancellationToken cancellationToken = default)
    {
        try
        {
            document.TryGetValue("Id", out object? id);
            document[nameof(ProjectionDocument.PartitionKey)] = partitionKey;
            document[nameof(ProjectionDocument.UpdatedAt)] = updatedAt;

            await _client.IndexAsync(
                new IndexRequest<Dictionary<string, object?>>(document, indexDescriptor.IndexName, id: id?.ToString())
                {
                    Routing = new Routing(partitionKey),
                    Refresh = Refresh.False
                },
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to upsert Document with {@Id} ({@Index})", document[_projectionDocumentSchema.KeyColumnName], indexDescriptor.IndexName
            );

            throw;
        }
    }

    protected override async Task<ProjectionQueryResult<Dictionary<string, object?>>> QueryInternal(
        ProjectionOperationIndexDescriptor indexDescriptor,
        ProjectionQuery projectionQuery,
        string? partitionKey = null,
        CancellationToken cancellationToken = default
    ) {
        try
        {
            var result = await _client.SearchAsync<Dictionary<string, object?>>(
                request =>
                {
                    request = request.Index(indexDescriptor.IndexName);

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

                    if (_disableRequestStreaming)
                    {
                        request = request.RequestConfiguration(conf => conf.DisableDirectStreaming());
                    }

                    return request;
                }
            );

            return new ProjectionQueryResult<Dictionary<string, object?>>
            {
                IndexName = indexDescriptor.IndexName,
                TotalRecordsFound = (int)result.Total,
                Records = result.Documents.Select(
                        x =>
                            new QueryResultDocument<Dictionary<string, object?>>
                            {
                                Document = DeserializeDictionary(x)
                            }
                    )
                    .ToList(),
                DebugInformation = result.DebugInformation
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error querying index ({@Index})", indexDescriptor.IndexName
            );

            throw;
        }
    }
    
    protected override async Task<IReadOnlyCollection<ProjectionIndexState>> QueryProjectionIndexStates(
        ProjectionQuery projectionQuery,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _client.SearchAsync<ProjectionIndexState>(
            request =>
            {
                request = request.Index(PROJECTION_INDEX_STATE_INDEX_NAME);

                request = request.TrackTotalHits(false);
                request = request.Query(q => ConstructSearchQuery(q, projectionQuery));
                request = request.Sort(s => s.Ascending(f => f.UpdatedAt));
                request = request.Skip(0);

                if (projectionQuery.Limit.HasValue)
                {
                    request = request.Take(projectionQuery.Limit.Value);
                }

                request = request.Routing(PROJECTION_INDEX_STATE_INDEX_NAME);
                
                if (_disableRequestStreaming)
                {
                    request = request.RequestConfiguration(conf => conf.DisableDirectStreaming());
                }

                return request;
            }
        );

        return result.Documents;
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
                if (listItem is string listItemString)
                {
                    resultList.Add(Guid.Parse(listItemString));
                }
                else
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

            textQueries.Add(
                new BoolQuery()
                {
                    Should = queries
                }
            );
        }

        if (projectionQuery.Filters == null || !projectionQuery.Filters.Any())
        {
            return searchDescriptor.Bool(
                q =>
                    new BoolQuery()
                    {
                        Should = textQueries
                    }
            );
        }

        // construct filters
        var filters = ElasticSearchFilterFactory.ConstructFilters(projectionQuery.Filters);

        return searchDescriptor.Bool(
            q =>
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
            SortOrder sortOrder = orderBy.Order.ToLower() == "asc" ? SortOrder.Ascending : SortOrder.Descending;

            sortDescriptor = sortDescriptor.Field(
                field =>
                {
                    var descriptor = field
                        .Field(new Field(orderBy.KeyPath))
                        .Order(sortOrder);

                    // add sorting statement for each nested level
                    for (var pathElementsNumber = 1; pathElementsNumber < keyPaths.Length; pathElementsNumber++)
                    {
                        descriptor = descriptor
                            .Nested(
                                nested =>
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
                                }
                            );
                    }

                    return descriptor;
                }
            );
        }

        return sortDescriptor;
    }
}
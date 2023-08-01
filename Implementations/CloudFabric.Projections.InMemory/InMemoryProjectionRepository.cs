using CloudFabric.Projections.Queries;
using Microsoft.Extensions.Logging;

namespace CloudFabric.Projections.InMemory;

public class InMemoryProjectionRepository<TProjectionDocument>
    : InMemoryProjectionRepository, IProjectionRepository<TProjectionDocument>
    where TProjectionDocument : ProjectionDocument
{
    public InMemoryProjectionRepository(ILoggerFactory loggerFactory) : 
        base(ProjectionDocumentSchemaFactory.FromTypeWithAttributes<TProjectionDocument>(), loggerFactory)
    {
    }

    public new async Task<TProjectionDocument?> Single(
        Guid id, 
        string partitionKey, 
        CancellationToken cancellationToken = default, 
        ProjectionOperationIndexSelector indexSelector = ProjectionOperationIndexSelector.ReadOnly
    ) {
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
    ) {
        var documentDictionary = ProjectionDocumentSerializer.SerializeToDictionary(document);
        return Upsert(documentDictionary, partitionKey, updatedAt, cancellationToken);
    }

    public new async Task<ProjectionQueryResult<TProjectionDocument>> Query(
        ProjectionQuery projectionQuery,
        string? partitionKey = null,
        CancellationToken cancellationToken = default,
        ProjectionOperationIndexSelector indexSelector = ProjectionOperationIndexSelector.ReadOnly
    ) {
        ProjectionQueryResult<Dictionary<string, object?>> recordsDictionary = await base.Query(projectionQuery, partitionKey, cancellationToken);

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

public class InMemoryProjectionRepository : ProjectionRepository
{
    private readonly ProjectionDocumentSchema _projectionDocumentSchema;

    /// <summary>
    /// Data storage 
    /// </summary>                     Index name         Item Id and PartitionKey                  Item properties and values
    private static readonly Dictionary<string, Dictionary<(string Id, string PartitionKey), Dictionary<string, object?>>> Storage = new();

    public InMemoryProjectionRepository(ProjectionDocumentSchema projectionDocumentSchema, ILoggerFactory loggerFactory)
        : base(projectionDocumentSchema, loggerFactory.CreateLogger<ProjectionRepository>())
    {
        _projectionDocumentSchema = projectionDocumentSchema;
        
        Storage.TryAdd(PROJECTION_INDEX_STATE_INDEX_NAME, new Dictionary<(string Id, string PartitionKey), Dictionary<string, object?>>());
    }

    protected override Task CreateIndex(string indexName, ProjectionDocumentSchema projectionDocumentSchema)
    {
        if (!Storage.ContainsKey(indexName))
        {
            Storage[indexName] = new();
        }
        
        return Task.CompletedTask;
    }

    public override async Task<Dictionary<string, object?>?> Single(
        Guid id, 
        string partitionKey, 
        CancellationToken cancellationToken = default, 
        ProjectionOperationIndexSelector indexSelector = ProjectionOperationIndexSelector.ReadOnly
    ) {
        var indexName = await GetIndexDescriptorForOperation(indexSelector, cancellationToken);
        
        var storage = Storage[indexName];
        
        return storage.GetValueOrDefault((id.ToString(), partitionKey)) ?? null;
    }

    public override async Task<ProjectionQueryResult<Dictionary<string, object?>>> Query(
        ProjectionQuery projectionQuery,
        string? partitionKey = null,
        CancellationToken cancellationToken = default,
        ProjectionOperationIndexSelector indexSelector = ProjectionOperationIndexSelector.ReadOnly
    ) {
        var indexName = await GetIndexDescriptorForOperation(indexSelector, cancellationToken);
        
        var storage = Storage[indexName];
        
        var result = storage
            .Where(x => string.IsNullOrEmpty(partitionKey) || x.Key.PartitionKey == partitionKey)
            .ToDictionary(k => k.Key, v => v.Value)
            .Values
            .AsEnumerable();

        var expression = projectionQuery.FiltersToExpression<Dictionary<string, object?>>();
        if (expression != null)
        {
            var lambda = expression.Compile();
            result = result.Where(lambda);
        }

        if (!string.IsNullOrWhiteSpace(projectionQuery.SearchText) && projectionQuery.SearchText != "*")
        {
            var searchableProperties = _projectionDocumentSchema.Properties
                .Where(x => x.IsSearchable)
                .Select(x => x.PropertyName);

            result = result.Where(x => 
                x.Any(
                    w => searchableProperties.Contains(w.Key) 
                        && w.Value is string 
                        && ((string)w.Value).ToLower().Contains(projectionQuery.SearchText.ToLower())
                )
            );
        }

        var totalCount = result.LongCount();

        result = result.Skip(projectionQuery.Offset);

        if (projectionQuery.Limit.HasValue)
        {
            result = result.Take(projectionQuery.Limit.Value);
        }

        return new ProjectionQueryResult<Dictionary<string, object?>>
        {
            IndexName = indexName,
            TotalRecordsFound = totalCount,
            Records = result.Select(
                    x => new QueryResultDocument<Dictionary<string, object?>>
                    {
                        Document = x
                    }
                )
                .ToList()
        };
    }

    
    protected override async Task<IReadOnlyCollection<ProjectionIndexState>> QueryProjectionIndexStates(
        ProjectionQuery projectionQuery,
        CancellationToken cancellationToken = default
    ) {
        var storage = Storage[PROJECTION_INDEX_STATE_INDEX_NAME];
        
        var result = storage
            .Where(x => x.Key.PartitionKey == PROJECTION_INDEX_STATE_INDEX_NAME)
            .ToDictionary(k => k.Key, v => v.Value)
            .Values
            .AsEnumerable();
        
        var expression = projectionQuery.FiltersToExpression<Dictionary<string, object?>>();
        if (expression != null)
        {
            var lambda = expression.Compile();
            result = result.Where(lambda);
        }

        result = result.Skip(projectionQuery.Offset);

        if (projectionQuery.Limit.HasValue)
        {
            result = result.Take(projectionQuery.Limit.Value);
        }

        return result
            .Select(doc => 
                ProjectionDocumentSerializer.DeserializeFromDictionary<ProjectionIndexState>(doc))
            .ToList()
            .AsReadOnly();
    }

    public override async Task Upsert(
        Dictionary<string, object?> document, 
        string partitionKey, 
        DateTime updatedAt, 
        CancellationToken cancellationToken = default,
        ProjectionOperationIndexSelector indexSelector = ProjectionOperationIndexSelector.Write
    ) {
        var keyValue = document[_projectionDocumentSchema.KeyColumnName];
        if (keyValue == null)
        {
            throw new ArgumentException("document.Id could not be null", _projectionDocumentSchema.KeyColumnName);
        }
        
        document.TryGetValue(nameof(ProjectionDocument.Id), out object? id);
        document[nameof(ProjectionDocument.PartitionKey)] = partitionKey;
        document[nameof(ProjectionDocument.UpdatedAt)] = updatedAt;

        var indexName = await GetIndexDescriptorForOperation(indexSelector, cancellationToken);
        
        Storage[indexName][(keyValue.ToString()!, partitionKey)] = document;
    }

    public override async Task Delete(
        Guid id, 
        string partitionKey, 
        CancellationToken cancellationToken = default, 
        ProjectionOperationIndexSelector indexSelector = ProjectionOperationIndexSelector.Write
    ) {
        var indexName = await GetIndexDescriptorForOperation(indexSelector, cancellationToken);
        
        Storage[indexName].Remove((id.ToString(), partitionKey));
    }

    public override async Task DeleteAll(
        string? partitionKey = null, 
        CancellationToken cancellationToken = default, 
        ProjectionOperationIndexSelector indexSelector = ProjectionOperationIndexSelector.Write
    ) {
        var indexName = await GetIndexDescriptorForOperation(indexSelector, cancellationToken);
        
        var storage = Storage[indexName];
        
        if (string.IsNullOrEmpty(partitionKey))
        {
            storage.Clear();
        }
        else
        {
            var objectsToRemove = storage.Where(x => x.Key.PartitionKey == partitionKey)
                .Select(x => x.Key);

            foreach (var objectToRemove in objectsToRemove)
            {
                storage.Remove(objectToRemove);
            }
        }
    }

    protected override Task<ProjectionIndexState?> GetProjectionIndexState(string schemaName, CancellationToken cancellationToken = default)
    {
        if(!Storage.ContainsKey(PROJECTION_INDEX_STATE_INDEX_NAME) || 
           !Storage[PROJECTION_INDEX_STATE_INDEX_NAME].ContainsKey((schemaName, PROJECTION_INDEX_STATE_INDEX_NAME))) {
            return Task.FromResult<ProjectionIndexState?>(null);
        }
        
        var indexState = Storage[PROJECTION_INDEX_STATE_INDEX_NAME][(schemaName, PROJECTION_INDEX_STATE_INDEX_NAME)];
        return Task.FromResult(ProjectionDocumentSerializer.DeserializeFromDictionary<ProjectionIndexState>(indexState));
    }
    
    protected override Task<ProjectionIndexState?> GetProjectionIndexState(CancellationToken cancellationToken = default)
    {
        return GetProjectionIndexState(_projectionDocumentSchema.SchemaName, cancellationToken);
    }

    protected override Task SaveProjectionIndexState(ProjectionIndexState state)
    {
        var key = (state.ProjectionName, PROJECTION_INDEX_STATE_INDEX_NAME);
        var value = ProjectionDocumentSerializer.SerializeToDictionary(state);

        if (!Storage[PROJECTION_INDEX_STATE_INDEX_NAME].ContainsKey(key))
        {
            Storage[PROJECTION_INDEX_STATE_INDEX_NAME].Add(key, value);
        }
        else
        {
            Storage[PROJECTION_INDEX_STATE_INDEX_NAME][key] = ProjectionDocumentSerializer.SerializeToDictionary(state);
        }
        
        return Task.CompletedTask;
    }
    
    public override Task UpdateProjectionRebuildStats(ProjectionIndexState state)
    {
        Storage[PROJECTION_INDEX_STATE_INDEX_NAME][(ProjectionDocumentSchema.SchemaName, PROJECTION_INDEX_STATE_INDEX_NAME)] =
            ProjectionDocumentSerializer.SerializeToDictionary(state);
        return Task.CompletedTask;
    }

}
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
        return Upsert(documentDictionary, partitionKey, updatedAt, cancellationToken, indexSelector);
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
    /// </summary>                     Index name         Item Id and PartitionKey                     Item properties and values
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
        var indexDescriptor = await GetIndexDescriptorForOperation(indexSelector, cancellationToken);
        
        var storage = Storage[indexDescriptor.IndexName];
        
        return storage.GetValueOrDefault((id.ToString(), partitionKey)) ?? null;
    }

    public override async Task Delete(
        Guid id, 
        string partitionKey, 
        CancellationToken cancellationToken = default, 
        ProjectionOperationIndexSelector indexSelector = ProjectionOperationIndexSelector.Write
    ) {
        var indexDescriptor = await GetIndexDescriptorForOperation(indexSelector, cancellationToken);
        
        Storage[indexDescriptor.IndexName].Remove((id.ToString(), partitionKey));
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
            if (partitionKey == null)
            {
                Storage[indexStatus.IndexName].Clear();
            }
            else
            {
                var allToRemove = Storage[indexStatus.IndexName].Where(kv => kv.Key.PartitionKey == partitionKey);

                foreach (var toRemove in allToRemove)
                {
                    Storage[indexStatus.IndexName].Remove(toRemove.Key);
                }
            }
        }
        
        indexState.IndexesStatuses.Clear();
        await SaveProjectionIndexState(indexState);
    }

    protected override async Task UpsertInternal(
        ProjectionOperationIndexDescriptor indexDescriptor,
        Dictionary<string, object?> document, 
        string partitionKey, 
        DateTime updatedAt, 
        CancellationToken cancellationToken = default
    ) {
        var keyValue = document[indexDescriptor.ProjectionDocumentSchema.KeyColumnName];
        if (keyValue == null)
        {
            throw new ArgumentException("document.Id could not be null", indexDescriptor.ProjectionDocumentSchema.KeyColumnName);
        }
        
        document.TryGetValue(nameof(ProjectionDocument.Id), out object? id);
        document[nameof(ProjectionDocument.PartitionKey)] = partitionKey;
        document[nameof(ProjectionDocument.UpdatedAt)] = updatedAt;

        Storage[indexDescriptor.IndexName][(keyValue.ToString()!, partitionKey)] = document;
    }

    
    protected override async Task<ProjectionQueryResult<Dictionary<string, object?>>> QueryInternal(
        ProjectionOperationIndexDescriptor indexDescriptor,
        ProjectionQuery projectionQuery,
        string? partitionKey = null,
        CancellationToken cancellationToken = default
    ) {
        var storage = Storage[indexDescriptor.IndexName];
        
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
            var searchableProperties = indexDescriptor.ProjectionDocumentSchema.Properties
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
            IndexName = indexDescriptor.IndexName,
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
}
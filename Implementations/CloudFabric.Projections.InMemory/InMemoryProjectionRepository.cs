using CloudFabric.Projections.Queries;

namespace CloudFabric.Projections.InMemory;

public class InMemoryProjectionRepository<TProjectionDocument>
    : InMemoryProjectionRepository, IProjectionRepository<TProjectionDocument>
    where TProjectionDocument : ProjectionDocument
{
    public InMemoryProjectionRepository() : base(ProjectionDocumentSchemaFactory
        .FromTypeWithAttributes<TProjectionDocument>())
    {
    }

    public new async Task<TProjectionDocument?> Single(Guid id, string partitionKey, CancellationToken cancellationToken = default)
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

    public new async Task<ProjectionQueryResult<TProjectionDocument>> Query(
        ProjectionQuery projectionQuery,
        string? partitionKey = null,
        CancellationToken cancellationToken = default
    )
    {
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

public class InMemoryProjectionRepository : IProjectionRepository
{
    private readonly ProjectionDocumentSchema _projectionDocumentSchema;

    /// <summary>
    /// Data storage 
    /// </summary>                     Schema name         Item Id and PartitionKey                  Item properties and values
    private static readonly Dictionary<string, Dictionary<(Guid Id, string PartitionKey), Dictionary<string, object?>>> Storage = new();

    public InMemoryProjectionRepository(ProjectionDocumentSchema projectionDocumentSchema)
    {
        _projectionDocumentSchema = projectionDocumentSchema;
    }

    public Task EnsureIndex(CancellationToken cancellationToken = default)
    {
        if (!Storage.ContainsKey(_projectionDocumentSchema.SchemaName))
        {
            Storage[_projectionDocumentSchema.SchemaName] = new();
        }
        
        return Task.CompletedTask;
    }

    public Task<Dictionary<string, object?>?> Single(Guid id, string partitionKey, CancellationToken cancellationToken = default)
    {
        var storage = Storage[_projectionDocumentSchema.SchemaName];
        
        return Task.FromResult(storage.GetValueOrDefault((id, partitionKey)) ?? null);
    }

    public Task<ProjectionQueryResult<Dictionary<string, object?>>> Query(
        ProjectionQuery projectionQuery,
        string? partitionKey = null,
        CancellationToken cancellationToken = default)
    {
        var storage = Storage[_projectionDocumentSchema.SchemaName];
        
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

        return Task.FromResult(
            new ProjectionQueryResult<Dictionary<string, object?>>
            {
                IndexName = "InMemory storage",
                TotalRecordsFound = totalCount,
                Records = result.Select(x => 
                    new QueryResultDocument<Dictionary<string, object?>>
                    {
                        Document = x
                    }
                ).ToList()
            }
        );
    }

    public Task Upsert(Dictionary<string, object?> document, string partitionKey, DateTime updatedAt, CancellationToken cancellationToken = default)
    {
        var keyValue = document[_projectionDocumentSchema.KeyColumnName];
        if (keyValue == null)
        {
            throw new ArgumentException("document.Id could not be null", _projectionDocumentSchema.KeyColumnName);
        }
        
        document.TryGetValue(nameof(ProjectionDocument.Id), out object? id);
        document[nameof(ProjectionDocument.PartitionKey)] = partitionKey;
        document[nameof(ProjectionDocument.UpdatedAt)] = updatedAt;

        Storage[_projectionDocumentSchema.SchemaName][(Guid.Parse(keyValue.ToString()), partitionKey)] = document;

        return Task.CompletedTask;
    }

    public Task Delete(Guid id, string partitionKey, CancellationToken cancellationToken = default)
    {
        Storage[_projectionDocumentSchema.SchemaName].Remove((id, partitionKey));
        return Task.CompletedTask;
    }

    public Task DeleteAll(string? partitionKey = null, CancellationToken cancellationToken = default)
    {
        var storage = Storage[_projectionDocumentSchema.SchemaName];
        
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

        return Task.CompletedTask;
    }
}
using System.Collections.Generic;
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

    public new async Task<TProjectionDocument?> Single(string id, string partitionKey, CancellationToken cancellationToken = default)
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

    public new async Task<IReadOnlyCollection<TProjectionDocument>> Query(
        ProjectionQuery projectionQuery,
        string? partitionKey = null,
        CancellationToken cancellationToken = default
    )
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

public class InMemoryProjectionRepository : IProjectionRepository
{
    private readonly ProjectionDocumentSchema _projectionDocumentSchema;
    private readonly Dictionary<(string Id, string PartitionKey), Dictionary<string, object?>> _storage = new();

    public InMemoryProjectionRepository(ProjectionDocumentSchema projectionDocumentSchema)
    {
        _projectionDocumentSchema = projectionDocumentSchema;
    }

    public Task<Dictionary<string, object?>?> Single(string id, string partitionKey, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_storage.GetValueOrDefault((id, partitionKey)) ?? null);
    }

    public Task<IReadOnlyCollection<Dictionary<string, object?>>> Query(
        ProjectionQuery projectionQuery,
        string? partitionKey = null,
        CancellationToken cancellationToken = default)
    {
        var expression = projectionQuery.FiltersToExpression<Dictionary<string, object?>>();

        if (expression == null)
        {
            return Task.FromResult(
                (IReadOnlyCollection<Dictionary<string, object?>>)_storage.Values.ToList().AsReadOnly());
        }

        var lambda = expression.Compile();
        var result = _storage
            .Where(x => string.IsNullOrEmpty(partitionKey) || x.Key.PartitionKey == partitionKey)
            .ToDictionary(k => k.Key, v => v.Value)
            .Values
            .Where(lambda)
            .ToList()
            .AsReadOnly();

        return Task.FromResult((IReadOnlyCollection<Dictionary<string, object?>>)result);
    }

    public Task Upsert(Dictionary<string, object?> document, string partitionKey, CancellationToken cancellationToken = default)
    {
        var keyValue = document[_projectionDocumentSchema.KeyColumnName];
        if (keyValue == null)
        {
            throw new ArgumentException("document.Id could not be null", _projectionDocumentSchema.KeyColumnName);
        }

        _storage[(keyValue.ToString(), partitionKey)] = document;

        return Task.CompletedTask;
    }

    public Task Delete(string id, string partitionKey, CancellationToken cancellationToken = default)
    {
        _storage.Remove((id, partitionKey));
        return Task.CompletedTask;
    }

    public Task DeleteAll(string partitionKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(partitionKey))
        {
            _storage.Clear();
        }
        else
        {
            var objectsToRemove = _storage.Where(x => x.Key.PartitionKey == partitionKey)
                .Select(x => x.Key);

            foreach (var objectToRemove in objectsToRemove)
            {
                _storage.Remove(objectToRemove);
            }
        }

        return Task.CompletedTask;
    }
}
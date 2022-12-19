using System.Collections.Generic;
using CloudFabric.Projections.Models;
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

    public Task Upsert(TProjectionDocument document, string partitionKey, CancellationToken cancellationToken = default)
    {
        var documentDictionary = ProjectionDocumentSerializer.SerializeToDictionary(document);
        return Upsert(documentDictionary, partitionKey, cancellationToken);
    }

    public new async Task<PagedList<TProjectionDocument>> Query(
        ProjectionQuery projectionQuery,
        string? partitionKey = null,
        CancellationToken cancellationToken = default
    )
    {
        PagedList<Dictionary<string, object?>> recordsDictionary = await base.Query(projectionQuery, partitionKey, cancellationToken);

        var records = new List<TProjectionDocument>();

        foreach (var dict in recordsDictionary.Records)
        {
            records.Add(
                ProjectionDocumentSerializer.DeserializeFromDictionary<TProjectionDocument>(dict)
            );
        }

        return new PagedList<TProjectionDocument>
        {
            Limit = recordsDictionary.Limit,
            Offset = recordsDictionary.Offset,
            TotalCount = recordsDictionary.TotalCount,
            Records = records
        };
    }
}

public class InMemoryProjectionRepository : IProjectionRepository
{
    private readonly ProjectionDocumentSchema _projectionDocumentSchema;
    private readonly Dictionary<(Guid Id, string PartitionKey), Dictionary<string, object?>> _storage = new();

    public InMemoryProjectionRepository(ProjectionDocumentSchema projectionDocumentSchema)
    {
        _projectionDocumentSchema = projectionDocumentSchema;
    }

    public Task<Dictionary<string, object?>?> Single(Guid id, string partitionKey, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_storage.GetValueOrDefault((id, partitionKey)) ?? null);
    }

    public Task<PagedList<Dictionary<string, object?>>> Query(
        ProjectionQuery projectionQuery,
        string? partitionKey = null,
        CancellationToken cancellationToken = default)
    {
        var result = _storage
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

        var totalCount = result.Count();

        result = result.Skip(projectionQuery.Offset)
            .Take(projectionQuery.Limit);

        return Task.FromResult(
            new PagedList<Dictionary<string, object?>>
            {
                Limit = projectionQuery.Limit,
                Offset = projectionQuery.Offset,
                TotalCount = totalCount,
                Records = result.ToList()
            }
        );
    }

    public Task Upsert(Dictionary<string, object?> document, string partitionKey, CancellationToken cancellationToken = default)
    {
        var keyValue = document[_projectionDocumentSchema.KeyColumnName];
        if (keyValue == null)
        {
            throw new ArgumentException("document.Id could not be null", _projectionDocumentSchema.KeyColumnName);
        }

        _storage[(Guid.Parse(keyValue.ToString()), partitionKey)] = document;

        return Task.CompletedTask;
    }

    public Task Delete(Guid id, string partitionKey, CancellationToken cancellationToken = default)
    {
        _storage.Remove((id, partitionKey));
        return Task.CompletedTask;
    }

    public Task DeleteAll(string? partitionKey = null, CancellationToken cancellationToken = default)
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
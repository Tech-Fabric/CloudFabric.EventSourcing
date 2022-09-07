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

    public new async Task<TProjectionDocument?> Single(string id, CancellationToken cancellationToken = default)
    {
        var document = await base.Single(id, cancellationToken);

        if (document == null) return null;

        return Deserialize(document);
    }

    public Task Upsert(TProjectionDocument document, CancellationToken cancellationToken = default)
    {
        var documentDictionary = new Dictionary<string, object?>();

        var propertyInfos = typeof(TProjectionDocument).GetProperties();
        foreach (var propertyInfo in propertyInfos)
        {
            documentDictionary[propertyInfo.Name] = propertyInfo.GetValue(document);
        }

        return Upsert(documentDictionary, cancellationToken);
    }

    public new async Task<IReadOnlyCollection<TProjectionDocument>> Query(
        ProjectionQuery projectionQuery,
        CancellationToken cancellationToken = default
    )
    {
        var recordsDictionary = await base.Query(projectionQuery, cancellationToken);

        var records = new List<TProjectionDocument>();

        foreach (var dict in recordsDictionary)
        {
            records.Add(Deserialize(dict));
        }

        return records;
    }

    private TProjectionDocument Deserialize(Dictionary<string, object?> document)
    {
        var documentTypedInstance = Activator.CreateInstance<TProjectionDocument>();

        foreach (var propertyName in document.Keys)
        {
            var propertyInfo = typeof(TProjectionDocument).GetProperty(propertyName);
            if (propertyInfo != null)
            {
                propertyInfo.SetValue(documentTypedInstance, document[propertyName]);
            }
        }

        return documentTypedInstance;
    }
}

public class InMemoryProjectionRepository : IProjectionRepository
{
    private readonly ProjectionDocumentSchema _projectionDocumentSchema;
    private readonly Dictionary<string, Dictionary<string, object?>> _storage = new();

    public InMemoryProjectionRepository(ProjectionDocumentSchema projectionDocumentSchema)
    {
        _projectionDocumentSchema = projectionDocumentSchema;
    }

    public Task<Dictionary<string, object?>?> Single(string id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_storage.GetValueOrDefault(id) ?? null);
    }

    public Task<IReadOnlyCollection<Dictionary<string, object?>>> Query(ProjectionQuery projectionQuery,
        CancellationToken cancellationToken = default)
    {
        var expression = projectionQuery.FiltersToExpression<Dictionary<string, object?>>();

        if (expression == null)
        {
            return Task.FromResult(
                (IReadOnlyCollection<Dictionary<string, object?>>)_storage.Values.ToList().AsReadOnly());
        }

        var lambda = expression.Compile();
        var result = _storage.Values.Where(lambda).ToList().AsReadOnly();

        return Task.FromResult((IReadOnlyCollection<Dictionary<string, object?>>)result);
    }

    public Task Upsert(Dictionary<string, object?> document, CancellationToken cancellationToken = default)
    {
        var keyValue = document[_projectionDocumentSchema.KeyColumnName];
        if (keyValue == null)
        {
            throw new ArgumentException("document.Id could not be null", _projectionDocumentSchema.KeyColumnName);
        }

        _storage[keyValue.ToString()] = document;

        return Task.CompletedTask;
    }

    public Task Delete(string id, CancellationToken cancellationToken = default)
    {
        _storage.Remove(id);
        return Task.CompletedTask;
    }

    public Task DeleteAll(CancellationToken cancellationToken = default)
    {
        _storage.Clear();
        return Task.CompletedTask;
    }
}
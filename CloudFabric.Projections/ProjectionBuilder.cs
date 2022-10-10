using CloudFabric.EventSourcing.EventStore;
using CloudFabric.Projections.Queries;

namespace CloudFabric.Projections;

public class ProjectionBuilder : IProjectionBuilder
{
    public ProjectionBuilder(IProjectionRepository repository)
    {
        var interfaces = GetType()
            .FindInterfaces(
                new System.Reflection.TypeFilter((type, _) =>
                    type.IsGenericType && typeof(IHandleEvent<>).IsAssignableFrom(type.GetGenericTypeDefinition())),
                null
            );

        HandledEventTypes = new HashSet<Type>(interfaces.Select(x => x.GenericTypeArguments.First()));

        Repository = repository;
    }

    public IProjectionRepository Repository { get; }
    public HashSet<Type> HandledEventTypes { get; }

    public async Task ApplyEvent(IEvent @event)
    {
        await (this as dynamic).On((dynamic)@event);
    }

    public async Task ApplyEvents(List<IEvent> events)
    {
        foreach (var e in events)
        {
            await ApplyEvent(e);
        }
    }

    protected Task UpsertDocument(Dictionary<string, object?> document, string partitionKey)
    {
        return Repository.Upsert(document, partitionKey);
    }

    protected Task UpdateDocument(Guid id, string partitionKey, Action<Dictionary<string, object?>> callback, Action? documentNotFound = null)
    {
        return UpdateDocument(id.ToString(), partitionKey, callback, documentNotFound);
    }

    protected Task UpdateDocument(string id, string partitionKey, Action<Dictionary<string, object?>> callback, Action? documentNotFound = null)
    {
        return UpdateDocument(
            id,
            partitionKey,
            document =>
            {
                callback(document);
                return Task.CompletedTask;
            },
            documentNotFound
        );
    }

    protected Task UpdateDocument(Guid id, string partitionKey, Func<Dictionary<string, object?>, Task> callback, Action? documentNotFound = null)
    {
        return UpdateDocument(id.ToString(), partitionKey, callback, documentNotFound);
    }

    private async Task UpdateDocument(
        string documentId,
        string partitionKey,
        Func<Dictionary<string, object?>, Task> callback,
        Action? documentNotFound = null)
    {
        if (documentId == null)
        {
            throw new ArgumentException("documentId should not be null", nameof(documentId));
        }

        Dictionary<string, object?>? document = await Repository.Single(documentId, partitionKey);

        if (document == null)
        {
            documentNotFound?.Invoke();
        }
        else
        {
            await callback(document);

            await Repository.Upsert(document, partitionKey);
        }
    }

    protected async Task UpdateDocuments(ProjectionQuery projectionQuery, string partitionKey, Action<Dictionary<string, object?>> callback)
    {
        var documents = await Repository.Query(projectionQuery);

        var updateTasks = documents.Select(document =>
        {
            callback(document);

            return Repository.Upsert(document, partitionKey);
        });

        await Task.WhenAll(updateTasks);
    }

    protected Task DeleteDocument(Guid id, string partitionKey)
    {
        return Repository.Delete(id.ToString(), partitionKey);
    }
}

public class ProjectionBuilder<TDocument> : IProjectionBuilder<ProjectionDocument>
    where TDocument : ProjectionDocument
{
    public ProjectionBuilder(IProjectionRepository<TDocument> repository)
    {
        var interfaces = GetType()
            .FindInterfaces(
                new System.Reflection.TypeFilter((type, _) =>
                    type.IsGenericType && typeof(IHandleEvent<>).IsAssignableFrom(type.GetGenericTypeDefinition())),
                null
            );

        HandledEventTypes = new HashSet<Type>(interfaces.Select(x => x.GenericTypeArguments.First()));

        Repository = repository;
    }

    public IProjectionRepository<TDocument> Repository { get; }
    public HashSet<Type> HandledEventTypes { get; }

    public async Task ApplyEvent(IEvent @event)
    {
        await (this as dynamic).On((dynamic)@event);
    }

    public async Task ApplyEvents(List<IEvent> events)
    {
        foreach (var e in events)
        {
            await ApplyEvent(e);
        }
    }

    protected Task UpsertDocument(TDocument document, string partitionKey)
    {
        return Repository.Upsert(document, partitionKey);
    }

    protected Task UpdateDocument(Guid id, string partitionKey, Action<TDocument> callback, Action? documentNotFound = null)
    {
        return UpdateDocument(id.ToString(), partitionKey, callback, documentNotFound);
    }

    protected Task UpdateDocument(string id, string partitionKey, Action<TDocument> callback, Action? documentNotFound = null)
    {
        return UpdateDocument(
            id,
            partitionKey,
            document =>
            {
                callback(document);
                return Task.CompletedTask;
            },
            documentNotFound
        );
    }

    protected Task UpdateDocument(Guid id, string partitionKey, Func<TDocument, Task> callback, Action? documentNotFound = null)
    {
        return UpdateDocument(id.ToString(), partitionKey, callback, documentNotFound);
    }

    private async Task UpdateDocument(
        string documentId,
        string partitionKey,
        Func<TDocument, Task> callback,
        Action? documentNotFound = null)
    {
        if (documentId == null)
        {
            throw new ArgumentException("documentId should not be null", nameof(documentId));
        }

        TDocument? document = await Repository.Single(documentId, partitionKey);

        if (document == null)
        {
            documentNotFound?.Invoke();
        }
        else
        {
            await callback(document);

            await Repository.Upsert(document, partitionKey);
        }
    }

    protected async Task UpdateDocuments(ProjectionQuery projectionQuery, string partitionKey, Action<TDocument> callback)
    {
        var documents = await Repository.Query(projectionQuery);

        var updateTasks = documents.Select(document =>
        {
            callback(document);

            return Repository.Upsert(document, partitionKey);
        });

        await Task.WhenAll(updateTasks);
    }

    protected Task DeleteDocument(Guid id, string partitionKey)
    {
        return Repository.Delete(id.ToString(), partitionKey);
    }
}
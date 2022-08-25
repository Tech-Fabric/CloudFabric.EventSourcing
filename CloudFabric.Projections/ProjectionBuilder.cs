using CloudFabric.EventSourcing.EventStore;
using CloudFabric.Projections.Queries;

namespace CloudFabric.Projections;

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

    protected Task UpsertDocument(TDocument document)
    {
        return Repository.Upsert(document);
    }

    protected Task UpdateDocument(Guid id, Action<TDocument> callback, Action? documentNotFound = null)
    {
        return UpdateDocument(id.ToString(), callback, documentNotFound);
    }

    protected Task UpdateDocument(string id, Action<TDocument> callback, Action? documentNotFound = null)
    {
        return UpdateDocument(id, document =>
        {
            callback(document);
            return Task.CompletedTask;
        }, documentNotFound);
    }

    protected Task UpdateDocument(Guid id, Func<TDocument, Task> callback, Action? documentNotFound = null)
    {
        return UpdateDocument(id.ToString(), callback, documentNotFound);
    }

    private async Task UpdateDocument(string documentId, Func<TDocument, Task> callback,
        Action? documentNotFound = null)
    {
        if (documentId == null)
        {
            throw new ArgumentException("documentId should not be null", nameof(documentId));
        }

        TDocument? document = await Repository.Single(documentId);

        if (document == null)
        {
            documentNotFound?.Invoke();
        }
        else
        {
            await callback(document);

            await Repository.Upsert(document);
        }
    }

    protected async Task UpdateDocuments(ProjectionQuery projectionQuery, Action<TDocument> callback)
    {
        var documents = await Repository.Query(projectionQuery);

        var updateTasks = documents.Select(document =>
        {
            callback(document);

            return Repository.Upsert(document);
        });

        await Task.WhenAll(updateTasks);
    }

    protected Task DeleteDocument(Guid id)
    {
        return Repository.Delete(id.ToString());
    }
}
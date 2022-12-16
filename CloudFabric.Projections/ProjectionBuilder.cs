using System.Reflection;
using CloudFabric.EventSourcing.EventStore;
using CloudFabric.Projections.Queries;

namespace CloudFabric.Projections;

public class ProjectionBuilder : IProjectionBuilder
{
    protected ProjectionBuilder(ProjectionRepositoryFactory projectionRepositoryFactory)
    {
        var interfaces = GetType()
            .FindInterfaces(
                new TypeFilter(
                    (type, _) =>
                        type.IsGenericType && typeof(IHandleEvent<>).IsAssignableFrom(type.GetGenericTypeDefinition())
                ),
                null
            );

        HandledEventTypes = new HashSet<Type>(interfaces.Select(x => x.GenericTypeArguments.First()));

        ProjectionRepositoryFactory = projectionRepositoryFactory;
    }

    protected readonly ProjectionRepositoryFactory ProjectionRepositoryFactory;
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

    protected Task UpsertDocument(
        ProjectionDocumentSchema projectionDocumentSchema,
        Dictionary<string, object?> document,
        string partitionKey,
        CancellationToken cancellationToken = default
    )
    {
        return ProjectionRepositoryFactory
            .GetProjectionRepository(projectionDocumentSchema)
            .Upsert(document, partitionKey, cancellationToken);
    }

    protected Task UpdateDocument(
        ProjectionDocumentSchema projectionDocumentSchema,
        Guid id,
        string partitionKey,
        Action<Dictionary<string, object?>> callback,
        Action? documentNotFound = null,
        CancellationToken cancellationToken = default
    )
    {
        return UpdateDocument(
            projectionDocumentSchema,
            id,
            partitionKey,
            document =>
            {
                callback(document);
                return Task.CompletedTask;
            },
            documentNotFound,
            cancellationToken
        );
    }

    protected async Task UpdateDocument(
        ProjectionDocumentSchema projectionDocumentSchema,
        Guid id,
        string partitionKey,
        Func<Dictionary<string, object?>, Task> callback,
        Action? documentNotFound = null,
        CancellationToken cancellationToken = default
    )
    {
        var repository = ProjectionRepositoryFactory
            .GetProjectionRepository(projectionDocumentSchema);

        Dictionary<string, object?>? document = await repository.Single(id, partitionKey, cancellationToken);

        if (document == null)
        {
            documentNotFound?.Invoke();
        }
        else
        {
            await callback(document);

            await repository.Upsert(document, partitionKey, cancellationToken);
        }
    }

    protected async Task UpdateDocuments(
        ProjectionDocumentSchema projectionDocumentSchema,
        ProjectionQuery projectionQuery,
        string partitionKey,
        Action<Dictionary<string, object?>> callback,
        CancellationToken cancellationToken = default
    )
    {
        var repository = ProjectionRepositoryFactory
            .GetProjectionRepository(projectionDocumentSchema);

        var documents = await repository.Query(projectionQuery, partitionKey, cancellationToken);

        var updateTasks = documents.Select(
            document =>
            {
                callback(document);

                return repository.Upsert(document, partitionKey, cancellationToken);
            }
        );

        await Task.WhenAll(updateTasks);
    }

    protected Task DeleteDocument(
        ProjectionDocumentSchema projectionDocumentSchema,
        Guid id,
        string partitionKey,
        CancellationToken cancellationToken = default
    )
    {
        var repository = ProjectionRepositoryFactory
            .GetProjectionRepository(projectionDocumentSchema);

        return repository.Delete(id, partitionKey, cancellationToken);
    }
}

public class ProjectionBuilder<TDocument> : IProjectionBuilder<ProjectionDocument>
    where TDocument : ProjectionDocument
{
    protected ProjectionBuilder(ProjectionRepositoryFactory projectionRepositoryFactory)
    {
        var interfaces = GetType()
            .FindInterfaces(
                new TypeFilter(
                    (type, _) =>
                        type.IsGenericType && typeof(IHandleEvent<>).IsAssignableFrom(type.GetGenericTypeDefinition())
                ),
                null
            );

        HandledEventTypes = new HashSet<Type>(interfaces.Select(x => x.GenericTypeArguments.First()));

        ProjectionRepositoryFactory = projectionRepositoryFactory;
    }

    protected readonly ProjectionRepositoryFactory ProjectionRepositoryFactory;

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

    protected Task UpsertDocument(TDocument document, string partitionKey, CancellationToken cancellationToken = default)
    {
        return ProjectionRepositoryFactory
            .GetProjectionRepository<TDocument>()
            .Upsert(document, partitionKey, cancellationToken);
    }

    protected Task UpdateDocument(
        Guid id,
        string partitionKey,
        Action<TDocument> callback,
        Action? documentNotFound = null,
        CancellationToken cancellationToken = default
    )
    {
        return UpdateDocument(
            id,
            partitionKey,
            document =>
            {
                callback(document);
                return Task.CompletedTask;
            },
            documentNotFound,
            cancellationToken
        );
    }

    private async Task UpdateDocument(
        Guid id,
        string partitionKey,
        Func<TDocument, Task> callback,
        Action? documentNotFound = null,
        CancellationToken cancellationToken = default
    )
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("id should not be null", nameof(id));
        }

        var repository = ProjectionRepositoryFactory
            .GetProjectionRepository<TDocument>();

        TDocument? document = await repository.Single(id, partitionKey, cancellationToken);

        if (document == null)
        {
            documentNotFound?.Invoke();
        }
        else
        {
            await callback(document);

            await repository.Upsert(document, partitionKey, cancellationToken);
        }
    }

    protected async Task UpdateDocuments(
        ProjectionQuery projectionQuery,
        string partitionKey,
        Action<TDocument> callback,
        CancellationToken cancellationToken = default
    )
    {
        var repository = ProjectionRepositoryFactory
            .GetProjectionRepository<TDocument>();

        var documents = await repository.Query(projectionQuery, partitionKey, cancellationToken);

        var updateTasks = documents.Select(
            document =>
            {
                callback(document);

                return repository.Upsert(document, partitionKey, cancellationToken);
            }
        );

        await Task.WhenAll(updateTasks);
    }

    protected Task DeleteDocument(
        Guid id,
        string partitionKey,
        CancellationToken cancellationToken = default
    )
    {
        return ProjectionRepositoryFactory
            .GetProjectionRepository<TDocument>()
            .Delete(id, partitionKey, cancellationToken);
    }
}

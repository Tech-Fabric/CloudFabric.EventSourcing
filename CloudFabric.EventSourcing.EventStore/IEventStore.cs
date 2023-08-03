using CloudFabric.EventSourcing.EventStore.Persistence;

namespace CloudFabric.EventSourcing.EventStore;

public interface IEventStore
{
    Task<EventStream> LoadStreamAsyncOrThrowNotFound(Guid streamId, string partitionKey, CancellationToken cancellationToken =  default);

    Task<EventStream> LoadStreamAsync(Guid streamId, string partitionKey, CancellationToken cancellationToken = default);

    Task<EventStream> LoadStreamAsync(Guid streamId, string partitionKey, int fromVersion, CancellationToken cancellationToken = default);

    Task<bool> AppendToStreamAsync(
        EventUserInfo eventUserInfo,
        Guid streamId,
        int expectedVersion,
        IEnumerable<IEvent> events,
        CancellationToken cancellationToken = default
    );

    Task Initialize(CancellationToken cancellationToken = default);

    Task DeleteAll(CancellationToken cancellationToken = default);

    Task<bool> HardDeleteAsync(Guid streamId, string partitionKey, CancellationToken cancellationToken = default);

    Task UpsertItem<T>(string id, string partitionKey, T item, CancellationToken cancellationToken = default);

    Task<T?> LoadItem<T>(string id, string partitionKey, CancellationToken cancellationToken = default);
}
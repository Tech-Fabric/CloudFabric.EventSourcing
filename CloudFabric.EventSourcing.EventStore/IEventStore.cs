using CloudFabric.EventSourcing.EventStore.Persistence;

namespace CloudFabric.EventSourcing.EventStore;

public interface IEventStore
{
    Task<EventStream> LoadStreamAsyncOrThrowNotFound(Guid streamId, string partitionKey);

    Task<EventStream> LoadStreamAsync(Guid streamId, string partitionKey);

    Task<EventStream> LoadStreamAsync(Guid streamId, string partitionKey, int fromVersion);

    Task<bool> AppendToStreamAsync(
        EventUserInfo eventUserInfo,
        Guid streamId,
        int expectedVersion,
        IEnumerable<IEvent> events
    );

    Task Initialize();

    Task DeleteAll();
}
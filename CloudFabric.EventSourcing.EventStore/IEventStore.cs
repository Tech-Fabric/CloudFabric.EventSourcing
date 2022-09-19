using CloudFabric.EventSourcing.EventStore.Persistence;

namespace CloudFabric.EventSourcing.EventStore;

public interface IEventStore
{
    Task<EventStream> LoadStreamAsyncOrThrowNotFound(string streamId, string partitionKey);

    Task<EventStream> LoadStreamAsync(string streamId, string partitionKey);

    Task<EventStream> LoadStreamAsync(string streamId, string partitionKey, int fromVersion);

    Task<bool> AppendToStreamAsync(
        EventUserInfo eventUserInfo,
        string streamId,
        string partitionKey,
        int expectedVersion,
        IEnumerable<IEvent> events
    );

    Task Initialize();

    Task DeleteAll();
}
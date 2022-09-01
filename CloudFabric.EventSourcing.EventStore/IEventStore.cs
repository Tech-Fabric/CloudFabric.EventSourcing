using CloudFabric.EventSourcing.EventStore.Persistence;

namespace CloudFabric.EventSourcing.EventStore;

public interface IEventStore
{
    Task<EventStream> LoadStreamAsyncOrThrowNotFound(string streamId);

    Task<EventStream> LoadStreamAsync(string streamId);

    Task<EventStream> LoadStreamAsync(string streamId, int fromVersion);

    Task<List<IEvent>> LoadEventsByDateAsync(DateTime? dateFrom, DateTime? dateTo = null);

    Task<bool> AppendToStreamAsync(
        EventUserInfo eventUserInfo,
        string streamId,
        int expectedVersion,
        IEnumerable<IEvent> events
    );

    Task Initialize();

    Task DeleteAll();
}
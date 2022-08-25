using CloudFabric.EventSourcing.EventStore;
using CloudFabric.EventSourcing.EventStore.Persistence;

namespace CloudFabric.EventSourcing.Domain;

public interface IAggregateRepository<T> where T : AggregateBase
{
    Task<T?> LoadAsync(string id, CancellationToken cancellationToken = default);
    Task<T> LoadAsyncOrThrowNotFound(string id, CancellationToken cancellationToken = default);
    Task<bool> SaveAsync(EventUserInfo eventUserInfo, T aggregate, CancellationToken cancellationToken = default);
}

public class AggregateRepository<T> : IAggregateRepository<T> where T : AggregateBase
{
    private readonly IEventStore _eventStore;

    public AggregateRepository(
        IEventStore eventStore
    )
    {
        _eventStore = eventStore;
    }

    public async Task<T?> LoadAsync(string id, CancellationToken cancellationToken = default)
    {
        if(string.IsNullOrEmpty(id)) {
            throw new ArgumentNullException(nameof(id));
        }

        var eventStream = await _eventStore.LoadStreamAsync(id);

        if (eventStream.Events.Any())
        {
            return (T?)Activator.CreateInstance(typeof(T), new object[] { eventStream.Events });
        }

        return null;
    }

    public async Task<T> LoadAsyncOrThrowNotFound(string id, CancellationToken cancellationToken = default)
    {
        if(string.IsNullOrEmpty(id)) {
            throw new ArgumentNullException(nameof(id));
        }

        var eventStream = await _eventStore.LoadStreamAsyncOrThrowNotFound(id);

        return (T)Activator.CreateInstance(typeof(T), new object[] { eventStream.Events });
    }

    public async Task<bool> SaveAsync(
        EventUserInfo eventUserInfo,
        T aggregate,
        CancellationToken cancellationToken = default
    )
    {
        if (aggregate.UncommittedEvents.Any())
        {
            var streamId = aggregate.Id.ToString();

            var eventsSavedSuccessfully = await _eventStore.AppendToStreamAsync(eventUserInfo, streamId,
                aggregate.Version,
                aggregate.UncommittedEvents);

            aggregate.OnChangesSaved();

            return eventsSavedSuccessfully;
        }

        return true;
    }
}
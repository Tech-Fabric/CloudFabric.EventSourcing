using CloudFabric.EventSourcing.EventStore;
using CloudFabric.EventSourcing.EventStore.Persistence;

namespace CloudFabric.EventSourcing.Domain;

public class AggregateRepository<T> : IAggregateRepository<T> where T : AggregateBase
{
    private readonly IEventStore _eventStore;

    public AggregateRepository(IEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    public async Task<T?> LoadAsync(string id, string partitionKey, CancellationToken cancellationToken = default)
    {
        if(string.IsNullOrEmpty(id))
        {
            throw new ArgumentNullException(nameof(id));
        }

        var eventStream = await _eventStore.LoadStreamAsync(id, partitionKey);

        if (eventStream.Events.Any())
        {
            return (T?)Activator.CreateInstance(typeof(T), new object[] { eventStream.Events });
        }

        return null;
    }

    public async Task<T> LoadAsyncOrThrowNotFound(string id, string partitionKey, CancellationToken cancellationToken = default)
    {
        if(string.IsNullOrEmpty(id))
        {
            throw new ArgumentNullException(nameof(id));
        }

        var eventStream = await _eventStore.LoadStreamAsyncOrThrowNotFound(id, partitionKey);

        return (T)Activator.CreateInstance(typeof(T), new object[] { eventStream.Events });
    }

    public async Task<bool> SaveAsync(EventUserInfo eventUserInfo, T aggregate, CancellationToken cancellationToken = default)
    {
        if (aggregate.UncommittedEvents.Any())
        {
            var streamId = aggregate.Id.ToString();

            var eventsSavedSuccessfully = await _eventStore.AppendToStreamAsync(
                eventUserInfo,
                streamId,
                aggregate.Version,
                aggregate.UncommittedEvents
            );

            aggregate.OnChangesSaved();

            return eventsSavedSuccessfully;
        }

        return true;
    }
}
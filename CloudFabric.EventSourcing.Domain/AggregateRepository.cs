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

    public async Task<T?> LoadAsync(Guid id, string partitionKey, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentNullException(nameof(id));
        }

        var eventStream = await _eventStore.LoadStreamAsync(id, partitionKey);

        if (!eventStream.Events.Any()) return null;

        return ConstructAggregateInstanceFromEventStream(eventStream);
    }

    public async Task<T> LoadAsyncOrThrowNotFound(Guid id, string partitionKey, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentNullException(nameof(id));
        }

        var eventStream = await _eventStore.LoadStreamAsyncOrThrowNotFound(id, partitionKey);

        return ConstructAggregateInstanceFromEventStream(eventStream);
    }

    private T ConstructAggregateInstanceFromEventStream(EventStream eventStream)
    {
        var firstEvent = eventStream.Events.First();

        // Support for derived types. The construction of generic T here will not work if 
        // our aggregate is one of many derived types of T. 
        // Hence we are storing exact aggregate type in each event to be able to construct 
        // exact derived type.
        if (!string.IsNullOrEmpty(firstEvent.AggregateType))
        {
            var type = Type.GetType(firstEvent.AggregateType);

            if (type != null)
            {
                return (T?)Activator.CreateInstance(type, new object[] { eventStream.Events }) ??
                       throw new InvalidOperationException(
                           "Unable to construct Aggregate instance of type " +
                           $"{firstEvent.AggregateType}"
                       );
            }
        }

        return (T?)Activator.CreateInstance(typeof(T), new object[] { eventStream.Events }) ??
               throw new InvalidOperationException(
                   "Unable to construct aggregate instance of type " +
                   $"{typeof(T).AssemblyQualifiedName}"
               );
    }

    public async Task<bool> SaveAsync(EventUserInfo eventUserInfo, T aggregate, CancellationToken cancellationToken = default)
    {
        if (aggregate.UncommittedEvents.Any())
        {
            var streamId = aggregate.Id;

            foreach (var e in aggregate.UncommittedEvents)
            {
                e.AggregateType = aggregate.GetType().AssemblyQualifiedName ?? "";
            }

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

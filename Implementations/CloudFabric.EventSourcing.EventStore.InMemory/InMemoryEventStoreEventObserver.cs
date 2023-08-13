using CloudFabric.Projections;
using Microsoft.Extensions.Logging;

namespace CloudFabric.EventSourcing.EventStore.InMemory;

public class InMemoryEventStoreEventObserver : EventsObserver
{
    private new readonly InMemoryEventStore _eventStore;

    public InMemoryEventStoreEventObserver(InMemoryEventStore eventStore, ILogger<InMemoryEventStoreEventObserver> logger): base(eventStore, logger)
    {
        _eventStore = eventStore;
    }


    public override Task StartAsync(string instanceName)
    {
        _eventStore.SubscribeToEventAdded(EventStoreOnEventAdded);
        return Task.CompletedTask;
    }

    public override Task StopAsync()
    {
        _eventStore.UnsubscribeFromEventAdded(EventStoreOnEventAdded);
        return Task.CompletedTask;
    }
}
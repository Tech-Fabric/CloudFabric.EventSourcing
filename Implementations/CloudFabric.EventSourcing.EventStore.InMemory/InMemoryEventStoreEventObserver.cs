using CloudFabric.Projections;

namespace CloudFabric.EventSourcing.EventStore.InMemory;

public class InMemoryEventStoreEventObserver : IEventsObserver
{
    private readonly InMemoryEventStore _eventStore;
    private Func<IEvent, Task> _eventHandler;

    public InMemoryEventStoreEventObserver(InMemoryEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    public void SetEventHandler(Func<IEvent, Task> eventHandler)
    {
        _eventHandler = eventHandler;
    }


    public Task StartAsync(string instanceName)
    {
        _eventStore.EventAdded += async (sender, args) => await EventStoreOnEventAdded(sender, args);
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        //_eventStore.EventAdded -= EventStoreOnEventAdded;
        return Task.CompletedTask;
    }

    private async Task EventStoreOnEventAdded(object? sender, EventAddedEventArgs e)
    {
        await _eventHandler(e.Event);
    }
}
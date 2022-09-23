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

    public async Task LoadAndHandleEventsAsync(
        string instanceName,
        string partitionKey,
        DateTime? dateFrom,
        Func<string, string, Task> onCompleted,
        Func<string, string, string, Task> onError
    )
    {
        try
        {
            var events = await _eventStore.LoadEventsAsync(partitionKey, dateFrom);

            foreach (var @event in events)
            {
                await EventStoreOnEventAdded(
                    this,
                    new EventAddedEventArgs { Event = @event }
                );
            }
        }
        catch (Exception ex)
        {
            await onError(instanceName, partitionKey, ex.InnerException?.Message ?? ex.Message);
            throw;
        }

        await onCompleted(instanceName, partitionKey);
    }

    public async Task LoadAndHandleEventsForDocumentAsync(string documentId, string partitionKey)
    {
        var stream = await _eventStore.LoadStreamAsync(documentId, partitionKey);

        foreach (var @event in stream.Events)
        {
            await EventStoreOnEventAdded(
                this,
                new EventAddedEventArgs { Event = @event }
            );
        }
    }

    private async Task EventStoreOnEventAdded(object? sender, EventAddedEventArgs e)
    {
        await _eventHandler(e.Event);
    }
}
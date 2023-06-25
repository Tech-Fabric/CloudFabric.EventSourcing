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
        _eventStore.SubscribeToEventAdded(EventStoreOnEventAdded);
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        _eventStore.UnsubscribeFromEventAdded(EventStoreOnEventAdded);
        return Task.CompletedTask;
    }

    public async Task ReplayEventsAsync(
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
                    @event
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

    public async Task ReplayEventsForDocumentAsync(Guid documentId, string partitionKey)
    {
        var stream = await _eventStore.LoadStreamAsync(documentId, partitionKey);

        foreach (var @event in stream.Events)
        {
            await EventStoreOnEventAdded(
                @event
            );
        }
    }

    public Task ReplayEventsAsync(
        string instanceName, 
        string? partitionKey, 
        DateTime? dateFrom, 
        int chunkSize = 250, 
        Func<IEvent, Task>? chunkProcessedCallback = null,
        CancellationToken cancellationToken = default
    )
    {
        throw new NotImplementedException();
    }

    public Task<EventStoreStatistics> GetEventStoreStatistics()
    {
        throw new NotImplementedException();
    }


    private async Task EventStoreOnEventAdded(IEvent e)
    {
        if (_eventHandler == null)
        {
            throw new InvalidOperationException(
                "Can't process an event: no eventHandler was set. Please call SetEventHandler before calling StartAsync.");
        }

        await _eventHandler(e);
    }
}
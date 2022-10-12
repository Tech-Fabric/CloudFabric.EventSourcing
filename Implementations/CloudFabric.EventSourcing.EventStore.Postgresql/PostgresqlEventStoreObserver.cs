using CloudFabric.Projections;

namespace CloudFabric.EventSourcing.EventStore.Postgresql;

public class PostgresqlEventStoreEventObserver : IEventsObserver
{
    private readonly PostgresqlEventStore _eventStore;
    private Func<IEvent, Task>? _eventHandler;

    public PostgresqlEventStoreEventObserver(PostgresqlEventStore eventStore)
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

    public async Task LoadAndHandleEventsForDocumentAsync(Guid documentId, string partitionKey)
    {
        var stream = await _eventStore.LoadStreamAsync(documentId, partitionKey);

        foreach (var @event in stream.Events)
        {
            await EventStoreOnEventAdded(@event);
        }
    }

    public async Task LoadAndHandleEventsAsync(
        string instanceName,
        string partitionKey,
        DateTime? dateFrom,
        Func<string, string, Task> onCompleted,
        Func<string, string, string, Task> onError
    )
    {
        Func<DateTime?, DateTime?, Task> handleEvents = 
            async (dateFrom, dateTo) =>
            {
                var events = await _eventStore.LoadEventsAsync(partitionKey, dateFrom, dateTo);

                foreach (var @event in events)
                {
                    await EventStoreOnEventAdded(@event);
                }
            };
        
        try
        {
            if (!dateFrom.HasValue)
            {
                await handleEvents(null, null);
            }
            else
            {            
                DateTime startDate = dateFrom.Value.Date;
                while (startDate <= DateTime.UtcNow.Date)
                {
                    DateTime endDate = startDate.AddDays(1);
                    await handleEvents(startDate, endDate);
                    
                    startDate = endDate;
                }
            }
        }
        catch (Exception ex)
        {
            await onError(instanceName, partitionKey, ex.InnerException?.Message ?? ex.Message);
            throw;
        }

        await onCompleted(instanceName, partitionKey);
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
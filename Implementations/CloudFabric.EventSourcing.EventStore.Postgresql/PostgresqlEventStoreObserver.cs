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

    public async Task LoadAndHandleEventsForDocumentAsync(string documentId)
    {
        var stream = await _eventStore.LoadStreamAsync(documentId);

        foreach (var @event in stream.Events)
        {
            await EventStoreOnEventAdded(@event);
        }
    }

    public async Task LoadAndHandleEventsAsync(DateTime? dateFrom)
    {
        Func<DateTime?, DateTime?, Task> handleEvents = 
            async (dateFrom, dateTo) =>
            {
                var events = await _eventStore.LoadEventsByDateAsync(dateFrom, dateTo);

                foreach (var @event in events)
                {
                    await EventStoreOnEventAdded(@event);
                }
            };
        
        // pagination
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
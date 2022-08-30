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
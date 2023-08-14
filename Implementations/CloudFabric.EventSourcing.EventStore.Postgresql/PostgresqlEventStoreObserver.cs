using CloudFabric.Projections;
using Microsoft.Extensions.Logging;

namespace CloudFabric.EventSourcing.EventStore.Postgresql;

public class PostgresqlEventStoreEventObserver : EventsObserver
{
    private new readonly PostgresqlEventStore _eventStore;

    public PostgresqlEventStoreEventObserver(
        PostgresqlEventStore eventStore,
        ILogger<PostgresqlEventStoreEventObserver> logger
    ): base(eventStore, logger)
    {
        _eventStore = eventStore;
    }

    
    public override Task StartAsync(string instanceName)
    {
        _logger.LogInformation("Starting {InstanceName}", instanceName);

        _eventStore.SubscribeToEventAdded(EventStoreOnEventAdded);
        return Task.CompletedTask;
    }

    public override Task StopAsync()
    {
        _logger.LogInformation("Stopping");

        _eventStore.UnsubscribeFromEventAdded(EventStoreOnEventAdded);
        return Task.CompletedTask;
    }
    
}
using CloudFabric.Projections;
using Microsoft.Extensions.Logging;

namespace CloudFabric.EventSourcing.EventStore.Postgresql;

public class PostgresqlEventStoreEventObserver : IEventsObserver
{
    private readonly PostgresqlEventStore _eventStore;
    private Func<IEvent, Task>? _eventHandler;
    
    private readonly ILogger<PostgresqlEventStoreEventObserver> _logger;

    public PostgresqlEventStoreEventObserver(
        PostgresqlEventStore eventStore,
        ILogger<PostgresqlEventStoreEventObserver> logger
    ) {
        _eventStore = eventStore;
        
        _logger = logger;
    }

    public void SetEventHandler(Func<IEvent, Task> eventHandler)
    {
        _eventHandler = eventHandler;
    }

    public Task StartAsync(string instanceName)
    {
        _logger.LogInformation("Starting {InstanceName}", instanceName);

        _eventStore.SubscribeToEventAdded(EventStoreOnEventAdded);
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        _logger.LogInformation("Stopping");

        _eventStore.UnsubscribeFromEventAdded(EventStoreOnEventAdded);
        return Task.CompletedTask;
    }

    public async Task ReplayEventsForOneDocumentAsync(Guid documentId, string partitionKey)
    {
        var stream = await _eventStore.LoadStreamAsync(documentId, partitionKey);

        foreach (var @event in stream.Events)
        {
            await EventStoreOnEventAdded(@event);
        }
    }

    public Task<EventStoreStatistics> GetEventStoreStatistics()
    {
        return _eventStore.GetStatistics();
    }

    public async Task ReplayEventsAsync(
        string instanceName, 
        string? partitionKey, 
        DateTime? dateFrom,
        int chunkSize = 250,
        Func<IEvent, Task>? chunkProcessedCallback = null,
        CancellationToken cancellationToken = default
    ) {
        _logger.LogInformation("Replaying events {InstanceName} from {DateFrom}",
            instanceName,
            dateFrom
        );
        
        var lastEventDateTime = dateFrom;
        var totalEventsProcessed = 0;
        
        while (true)
        {
            var chunk = await _eventStore.LoadEventsAsync(
                partitionKey, 
                lastEventDateTime, 
                chunkSize, 
                cancellationToken
            );

            if (chunk.Count <= 0)
            {
                break;
            }
            
            foreach (var @event in chunk)
            {
                await EventStoreOnEventAdded(@event);
            }
            
            var lastEvent = chunk.Last();
            lastEventDateTime = lastEvent.Timestamp;
            totalEventsProcessed += chunk.Count;
                
            _logger.LogInformation(
                "Replayed {ReplayedEventsCount} {InstanceName}, " +
                "last event timestamp: {LastEventDateTime}", 
                chunk.Count, 
                instanceName,
                lastEvent.Timestamp
            );

            if (chunkProcessedCallback != null)
            {
                await chunkProcessedCallback(lastEvent);
            }
            
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Cancellation requested. Processed {TotalEventsProcessed} {InstanceName}", 
                    totalEventsProcessed, 
                    instanceName
                );

                break;
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
using CloudFabric.EventSourcing.EventStore;
using Microsoft.Extensions.Logging;

namespace CloudFabric.Projections;

public abstract class EventsObserver
{
    protected Func<IEvent, Task>? _eventHandler;
    protected readonly ILogger<EventsObserver> _logger;
    protected readonly IEventStore _eventStore;

    protected EventsObserver(IEventStore eventStore, ILogger<EventsObserver> logger)
    {
        _eventStore = eventStore;
        _logger = logger;
    }

    public void SetEventHandler(Func<IEvent, Task> eventHandler)
    {
        _eventHandler = eventHandler;
    }

    public abstract Task StartAsync(string instanceName);

    public abstract Task StopAsync();

    public virtual async Task ReplayEventsForOneDocumentAsync(Guid documentId, string partitionKey)
    {
        var stream = await _eventStore.LoadStreamAsync(documentId, partitionKey);

        foreach (var @event in stream.Events)
        {
            await EventStoreOnEventAdded(@event);
        }
    }

    public virtual Task<EventStoreStatistics> GetEventStoreStatistics()
    {
        return _eventStore.GetStatistics();
    }

    /// <summary>
    /// Reads all events and runs event handlers on them (basically, "replays" those events). Needed for projections (materialized views)
    /// rebuild.
    /// </summary>
    /// <param name="instanceName">Used for tracking purposes. You can pass machineName or processId here.</param>
    /// <param name="partitionKey">PartitionKey to filter all events by.</param>
    /// <param name="dateFrom">Skip events which happened prior to this date.</param>
    /// <param name="chunkSize">How many events to load at a time.</param>
    /// <param name="chunkProcessedCallback">Function that will be called after each chunk of `chunkSize` is processed. Arguments: number of processed events (can be lower than chunkSize), last processed event</param>
    /// <param name="cancellationToken">This is a long-running operation, so make sure to pass correct CancellationToken here.</param>
    /// <returns></returns>
    public virtual async Task ReplayEventsAsync(
        string instanceName, 
        string? partitionKey, 
        DateTime? dateFrom,
        int chunkSize = 250,
        Func<int, IEvent, Task>? chunkProcessedCallback = null,
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
                await chunkProcessedCallback(chunk.Count, lastEvent);
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

    protected async Task EventStoreOnEventAdded(IEvent e)
    {
        if (_eventHandler == null)
        {
            throw new InvalidOperationException(
                "Can't process an event: no eventHandler was set. Please call SetEventHandler before calling StartAsync.");
        }

        await _eventHandler(e);
    }
}
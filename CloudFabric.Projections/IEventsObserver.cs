using CloudFabric.EventSourcing.EventStore;

namespace CloudFabric.Projections;

public interface IEventsObserver
{
    Task StartAsync(string instanceName);

    Task StopAsync();

    void SetEventHandler(Func<IEvent, Task> eventHandler);

    Task ReplayEventsForOneDocumentAsync(Guid documentId, string partitionKey);

    /// <summary>
    /// Reads all events and runs event handlers on them (basically, "replays" those events). Needed for projections (materialized views)
    /// rebuild.
    /// </summary>
    /// <param name="instanceName">Used for tracking purposes. You can pass machineName or processId here.</param>
    /// <param name="partitionKey">PartitionKey to filter all events by.</param>
    /// <param name="dateFrom">Skip events which happened prior to this date.</param>
    /// <param name="chunkSize">How many events to load at a time.</param>
    /// <param name="chunkProcessedCallback">Function that will be called after each chunk of `chunkSize` is processed.</param>
    /// <param name="cancellationToken">This is a long-running operation, so make sure to pass correct CancellationToken here.</param>
    /// <returns></returns>
    Task ReplayEventsAsync(
        string instanceName, 
        string? partitionKey, 
        DateTime? dateFrom,
        int chunkSize = 250,
        Func<IEvent, Task>? chunkProcessedCallback = null,
        CancellationToken cancellationToken = default
    );

    Task<EventStoreStatistics> GetEventStoreStatistics();
}
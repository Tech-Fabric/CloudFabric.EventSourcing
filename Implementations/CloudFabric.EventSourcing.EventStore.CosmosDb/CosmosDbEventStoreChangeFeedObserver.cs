using System.Text.Json.Serialization;
using CloudFabric.EventSourcing.EventStore.Persistence;
using CloudFabric.Projections;
using Microsoft.Azure.Cosmos;

namespace CloudFabric.EventSourcing.EventStore.CosmosDb;

public record Change : EventWrapper
{
    [JsonPropertyName("_lsn")] 
    public long LogicalSequenceNumber { get; set; }

    [JsonPropertyName("_ts")] 
    public long TimeStamp { get; set; }
}

public class CosmosDbEventStoreChangeFeedObserver : IEventsObserver
{
    protected readonly CosmosClient _eventsClient;
    protected readonly string _eventsContainerId;
    protected readonly string _eventsDatabaseId;


    protected readonly CosmosClient _leaseClient;
    protected readonly string _leaseContainerId;
    protected readonly string _leaseDatabaseId;
    private ChangeFeedProcessor _changeFeedProcessor;
    private Func<IEvent, Task<bool>> _eventHandler;

    private string _processorName;

    public CosmosDbEventStoreChangeFeedObserver(
        CosmosClient eventsClient,
        string eventsDatabaseId,
        string eventsContainerId,
        CosmosClient leaseClient,
        string leaseDatabaseId,
        string leaseContainerId,
        string processorName
    )
    {
        _eventsClient = eventsClient;
        _eventsDatabaseId = eventsDatabaseId;
        _eventsContainerId = eventsContainerId;

        _leaseClient = leaseClient;
        _leaseDatabaseId = leaseDatabaseId;
        _leaseContainerId = leaseContainerId;

        _processorName = processorName;
    }

    public void SetEventHandler(Func<IEvent, Task<bool>> eventHandler)
    {
        _eventHandler = eventHandler;
    }

    public Task StartAsync(string instanceName)
    {
        Container eventContainer = _eventsClient.GetContainer(_eventsDatabaseId, _eventsContainerId);
        Container leaseContainer = _leaseClient.GetContainer(_leaseDatabaseId, _leaseContainerId);

        // start with events at a specific time
        // https://docs.microsoft.com/en-us/azure/cosmos-db/change-feed-processor
        //var myTime = DateTimeOffset.FromUnixTimeSeconds(_epochStartTime).UtcDateTime;

        _changeFeedProcessor = eventContainer
            .GetChangeFeedProcessorBuilder<Change>(_processorName, HandleChangesAsync)
            .WithInstanceName(instanceName)
            .WithLeaseContainer(leaseContainer)
            .WithStartTime(DateTime.UtcNow.AddMinutes(-50))
            //.WithStartTime(myTime)
            .Build();

        return _changeFeedProcessor.StartAsync();
    }

    public Task StopAsync()
    {
        return _changeFeedProcessor.StopAsync();
    }

    private async Task HandleChangesAsync(IReadOnlyCollection<Change> changes, CancellationToken cancellationToken)
    {
        foreach (var change in changes)
        {
            var @event = change.GetEvent();

            await _eventHandler(@event);
        }
    }
}
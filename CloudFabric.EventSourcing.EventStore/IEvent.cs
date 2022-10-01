namespace CloudFabric.EventSourcing.EventStore;

public interface IEvent
{
    DateTime Timestamp { get; init; }

    string PartitionKey { get; set; }
}
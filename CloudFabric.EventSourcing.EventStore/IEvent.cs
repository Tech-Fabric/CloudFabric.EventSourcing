namespace CloudFabric.EventSourcing.EventStore;

public interface IEvent
{
    public Guid? AggregateId { get; set; }
    
    DateTime Timestamp { get; init; }

    string PartitionKey { get; set; }
    
    string AggregateType { get; set; }
}
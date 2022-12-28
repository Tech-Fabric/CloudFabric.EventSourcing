using CloudFabric.EventSourcing.EventStore;

namespace CloudFabric.EventSourcing.Tests.Domain.Events;

public record OrderItemRemoved : Event
{
    public OrderItemRemoved() { }
    
    public OrderItemRemoved(Guid id, string name, string partitionKey)
    {
        AggregateId = id;
        Name = name;
        PartitionKey = partitionKey;
    }

    public string Name { get; init; }
}
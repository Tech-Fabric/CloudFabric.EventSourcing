using CloudFabric.EventSourcing.EventStore;
using CloudFabric.EventSourcing.Tests.Domain.ValueObjects;

namespace CloudFabric.EventSourcing.Tests.Domain.Events;

public record OrderItemRemoved : Event
{
    public OrderItemRemoved() { }
    
    public OrderItemRemoved(Guid id, OrderItem item, string partitionKey)
    {
        AggregateId = id;
        Item = item;
        PartitionKey = partitionKey;
    }

    public OrderItem Item { get; init; }
}
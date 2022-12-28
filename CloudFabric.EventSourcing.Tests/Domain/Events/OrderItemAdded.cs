using CloudFabric.EventSourcing.EventStore;
using CloudFabric.EventSourcing.Tests.Domain.ValueObjects;

namespace CloudFabric.EventSourcing.Tests.Domain.Events;

public record OrderItemAdded : Event
{
    public OrderItemAdded() { }
    
    public OrderItemAdded(Guid id, OrderItem item, string partitionKey)
    {
        AggregateId = id;
        Item = item;
        PartitionKey = partitionKey;
    }

    public OrderItem Item { get; init; }
}
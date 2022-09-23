using CloudFabric.EventSourcing.EventStore;
using CloudFabric.EventSourcing.Tests.Domain.ValueObjects;

namespace CloudFabric.EventSourcing.Tests.Domain.Events;

public record OrderItemAdded : Event
{
    public OrderItemAdded(Guid id, OrderItem item, string partitionKey)
    {
        Id = id;
        Item = item;
        PartitionKey = partitionKey;
    }

    public Guid Id { get; init;}

    public OrderItem Item { get; init; }
}
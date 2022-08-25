using CloudFabric.EventSourcing.EventStore;
using CloudFabric.EventSourcing.Tests.Domain.ValueObjects;

namespace CloudFabric.EventSourcing.Tests.Domain.Events;

public record OrderItemAdded : Event
{
    public OrderItemAdded(Guid id, OrderItem item)
    {
        Id = id;
        Item = item;
    }

    public Guid Id { get; init;}

    public OrderItem Item { get; init; }
}
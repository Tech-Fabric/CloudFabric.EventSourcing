using CloudFabric.EventSourcing.EventStore;
using CloudFabric.EventSourcing.Tests.Domain.ValueObjects;

namespace CloudFabric.EventSourcing.Tests.Domain.Events;

public record OrderPlaced : Event
{
    public OrderPlaced(Guid id, string orderName, List<OrderItem> items)
    {
        Id = id;
        OrderName = orderName;
        Items = items;
    }

    public Guid Id { get; init; }
    public string OrderName { get; init; }
    public List<OrderItem> Items { get; init; }
}
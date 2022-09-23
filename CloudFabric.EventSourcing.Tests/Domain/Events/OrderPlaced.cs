using CloudFabric.EventSourcing.EventStore;
using CloudFabric.EventSourcing.Tests.Domain.ValueObjects;

namespace CloudFabric.EventSourcing.Tests.Domain.Events;

public record OrderPlaced : Event
{
    public OrderPlaced(Guid id, string orderName, string partitionKey, List<OrderItem> items)
    {
        Id = id;
        OrderName = orderName;
        PartitionKey = partitionKey;
        Items = items;
    }

    public Guid Id { get; init; }
    public string OrderName { get; init; }
    public List<OrderItem> Items { get; init; }
}
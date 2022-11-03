using CloudFabric.EventSourcing.EventStore;
using CloudFabric.EventSourcing.Tests.Domain.ValueObjects;

namespace CloudFabric.EventSourcing.Tests.Domain.Events;

public record OrderPlaced : Event
{
    public OrderPlaced(Guid id, string orderName, string partitionKey, List<OrderItem> items, Guid createdById, string createdByEmail)
    {
        Id = id;
        OrderName = orderName;
        PartitionKey = partitionKey;
        Items = items;
        CreatedById = createdById;
        CreatedByEmail = createdByEmail;
    }

    public Guid Id { get; init; }
    public string OrderName { get; init; }
    public List<OrderItem> Items { get; init; }
    public Guid CreatedById { get; set; }
    public string CreatedByEmail { get; set; }
}
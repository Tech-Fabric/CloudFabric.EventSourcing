using CloudFabric.EventSourcing.EventStore;
using CloudFabric.EventSourcing.Tests.Domain.ValueObjects;

namespace CloudFabric.EventSourcing.Tests.Domain.Events;

public record OrderPlaced : Event
{
    public OrderPlaced() { }
    
    public OrderPlaced(Guid id, string orderName, string partitionKey, List<OrderItem> items, Guid createdById, string createdByEmail)
    {
        AggregateId = id;
        OrderName = orderName;
        PartitionKey = partitionKey;
        Items = items;
        CreatedById = createdById;
        CreatedByEmail = createdByEmail;
    }

    public string OrderName { get; init; }
    public List<OrderItem> Items { get; init; }
    public Guid CreatedById { get; set; }
    public string CreatedByEmail { get; set; }
}
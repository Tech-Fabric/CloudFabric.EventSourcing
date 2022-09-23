using CloudFabric.EventSourcing.EventStore;

namespace CloudFabric.EventSourcing.Tests.Domain.Events;

public record OrderItemRemoved : Event
{
    public OrderItemRemoved(Guid id, string name, string partitionKey)
    {
        Id = id;
        Name = name;
        PartitionKey = partitionKey;
    }

    public Guid Id { get; init; }

    public string Name { get; init; }
}
using CloudFabric.EventSourcing.EventStore;

namespace CloudFabric.EventSourcing.Tests.Domain.Events;

public record OrderItemRemoved : Event
{
    public OrderItemRemoved(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    public Guid Id { get; init; }

    public string Name { get; init; }
}
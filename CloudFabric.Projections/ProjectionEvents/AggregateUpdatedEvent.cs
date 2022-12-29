using CloudFabric.EventSourcing.EventStore;

namespace CloudFabric.Projections;

/// <summary>
/// Is used only for projection updates.
/// It will not be applied to aggregate domain model.
/// </summary>
/// <typeparam name="T">AggregateType</typeparam>
public record AggregateUpdatedEvent<T> : Event
{
    public DateTime UpdatedAt { get; set; }
    
    public AggregateUpdatedEvent()
    {
        
    }
    
    public AggregateUpdatedEvent(DateTime updatedAt)
    {
        UpdatedAt = updatedAt;
    }
}

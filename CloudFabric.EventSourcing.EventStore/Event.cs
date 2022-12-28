using System.Diagnostics;

namespace CloudFabric.EventSourcing.EventStore;

[DebuggerStepThrough]
public record Event : IEvent
{
    public Guid? AggregateId { get; set; }
    
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    public string PartitionKey { get; set; }
    
    public string AggregateType { get; set; }
}
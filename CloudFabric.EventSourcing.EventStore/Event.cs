using System.Diagnostics;

namespace CloudFabric.EventSourcing.EventStore;

[DebuggerStepThrough]
public record Event : IEvent
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    public string PartitionKey { get; set; }
}
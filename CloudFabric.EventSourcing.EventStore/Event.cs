using System.Diagnostics;

namespace CloudFabric.EventSourcing.EventStore;

[DebuggerStepThrough]
public record Event : IEvent
{
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}
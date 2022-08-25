namespace CloudFabric.EventSourcing.EventStore;

public interface IEvent
{
    DateTime Timestamp { get; }
}
namespace CloudFabric.EventSourcing.EventStore;

public class EventStoreStatistics
{
    public long TotalEventsCount { get; set; }
    public DateTime? FirstEventCreatedAt { get; set; }
    public DateTime? LastEventCreatedAt { get; set; }
}

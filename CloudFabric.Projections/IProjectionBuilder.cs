using CloudFabric.EventSourcing.EventStore;

namespace CloudFabric.Projections;

public interface IProjectionBuilder<TProjectionDocument> where TProjectionDocument : ProjectionDocument
{
    public HashSet<Type> HandledEventTypes { get; }

    Task ApplyEvent(IEvent @event, string partitionKey);

    Task ApplyEvents(List<IEvent> events, string partitionKey);
}
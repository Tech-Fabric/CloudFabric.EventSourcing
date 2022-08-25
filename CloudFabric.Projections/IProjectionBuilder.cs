using CloudFabric.EventSourcing.EventStore;

namespace CloudFabric.Projections;

public interface IProjectionBuilder<TProjectionDocument> where TProjectionDocument : ProjectionDocument
{
    public HashSet<Type> HandledEventTypes { get; }

    Task ApplyEvent(IEvent @event);

    Task ApplyEvents(List<IEvent> events);
}
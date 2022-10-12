namespace CloudFabric.EventSourcing.EventStore;

public class EventStream
{
    private readonly List<IEvent> _events;

    public EventStream(Guid id, int version, IEnumerable<IEvent> events)
    {
        Id = id;
        Version = version;
        _events = events.ToList();
    }

    public Guid Id { get; private set; }

    public int Version { get; private set; }

    public IEnumerable<IEvent> Events
    {
        get { return _events; }
    }
}
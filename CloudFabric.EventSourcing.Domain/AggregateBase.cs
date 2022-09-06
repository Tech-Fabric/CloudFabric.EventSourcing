using CloudFabric.EventSourcing.EventStore;

namespace CloudFabric.EventSourcing.Domain;

/// <summary>
/// See Martin Fowler's definition on aggregates. In addition to that, our aggregates consist of events stream 
/// and don't have a state storage - they are constructred from list of events passed into constructor.
/// 
/// All modifications (mutations) to a state are made by triggering events.
/// 
/// All properties of an aggregate should have private setters.
/// 
/// See example in Order aggregate test case in CloudFabric.EventSourcing.EventStore.Tests\Domain\Order.cs
/// </summary>
/// <see cref="https://www.martinfowler.com/bliki/DDD_Aggregate.html"/>
/// <seealso cref="CloudFabric.EventSourcing.EventStore.Tests\Domain\Order.cs"/>
public abstract class AggregateBase
{
    public virtual string Id { get; protected set; }

    public AggregateBase()
    {
    }

    public AggregateBase(IEnumerable<IEvent> events)
    {
        if (events == null)
        {
            throw new Exception("Aggregate should not be constructed with null events list");
        }

        foreach (var @event in events)
        {
            if (@event == null)
            {
                throw new Exception("event is null");
            }

            RaiseEvent(@event);
            Version += 1;
        }
    }

    /// <summary>
    /// Number of events which happened to mutate this aggregate into it's current state.
    /// </summary>
    public int Version { get; internal set; }

    /// <summary>
    /// Changes - new events that were not stored to persistance yet.
    /// </summary>
    public List<IEvent> UncommittedEvents { get; protected set; } = new List<IEvent>();

    public void OnChangesSaved()
    {
        Version += UncommittedEvents.Count;
        UncommittedEvents.Clear();
    }

    protected void Apply(IEvent @event)
    {
        UncommittedEvents.Add(@event);
        RaiseEvent(@event);
    }

    protected virtual void RaiseEvent(IEvent @event)
    {
        ((dynamic)this).On((dynamic)@event);
    }
}
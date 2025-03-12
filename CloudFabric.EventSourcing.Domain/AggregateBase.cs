using System.Text;
using CloudFabric.EventSourcing.EventStore;

namespace CloudFabric.EventSourcing.Domain;

/// <summary>
/// See Martin Fowler's definition on aggregates. In addition to that, our aggregates consist of events stream 
/// and don't have a state storage - they are constructed from list of events passed into constructor.
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
    public virtual Guid Id { get; protected set; }

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
    /// Aggregate's id is always a Guid.
    /// 
    /// That is not always handy when we need an aggregate with unique identifier as its id.
    /// Good example is UserEmailAddress domain aggregate. We need to be able to query database by email address string,
    /// but the only way to query an aggregate is by Guid.
    ///
    /// For such situations we would override UserEmailAddress.Id and make it return the value of HashStringToGuid(emailAddress).
    ///
    /// When querying we can simply create a new instance of an aggregate and use its id.
    ///
    /// </summary>
    /// <example>
    /// public override Guid Id
    /// {
    ///    get => HashStringToGuid(emailAddress)
    /// }
    /// </example>
    ///
    /// /// <example>
    /// var emailLookup = new UserEmailAddress("test@test.com");
    /// var existingEmailAddress = emailAddressRepository.Load(emailLookup.Id);
    /// </example>
    /// <param name="stringToHash"></param>
    /// <returns></returns>
    public static Guid HashStringToGuid(string stringToHash)
    {
        // Super fast non-cryptographic hash function: 
        // https://cyan4973.github.io/xxHash/
        // 128 version is used because that's what Guid uses for it's value
        var hash = new System.IO.Hashing.XxHash128();

        hash.Append(Encoding.UTF8.GetBytes(stringToHash));

        return new Guid(hash.GetCurrentHash());
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

    public abstract string PartitionKey { get; }

    protected void Apply(IEvent @event)
    {
        RaiseEvent(@event);
     
        // aggregates should not bother assigning those to events
        @event.AggregateId = Id;
        @event.PartitionKey = PartitionKey;
        
        UncommittedEvents.Add(@event);
    }

    protected virtual void RaiseEvent(IEvent @event)
    {
        ((dynamic)this).On((dynamic)@event);
    }
}
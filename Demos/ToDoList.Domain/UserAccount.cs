using CloudFabric.EventSourcing.Domain;
using CloudFabric.EventSourcing.EventStore;

using ToDoList.Domain.Events.UserAccounts;

namespace ToDoList.Domain;

public class UserAccount : AggregateBase
{
    public string FirstName { get; protected set; }

    public string HashedPassword { get; protected set; }

    public string PasswordUpdatedAt { get; protected set; }

    public UserAccount(IEnumerable<IEvent> events) : base(events)
    {
    }

    public UserAccount(string id, string firstName, string hashedPassword)
    {
        Apply(new UserAccountRegistered(id, firstName, hashedPassword));
    }

    public void UpdatePassword(string newHashedPassword)
    {
        Apply(new UserAccountPasswordUpdated(newHashedPassword));
    }

    #region Event Handlers

    protected void On(UserAccountRegistered @event)
    {
        Id = @event.Id;
        FirstName = @event.FirstName;
        HashedPassword = @event.HashedPassword;
    }

    protected void On(UserAccountPasswordUpdated @event)
    {
        HashedPassword = @event.NewHashedPassword;
    }


    protected override void RaiseEvent(IEvent @event)
    {
        ((dynamic)this).On((dynamic)@event);
    }

    #endregion
}

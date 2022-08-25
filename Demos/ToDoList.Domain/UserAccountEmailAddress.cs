using CloudFabric.EventSourcing.Domain;
using CloudFabric.EventSourcing.EventStore;

using ToDoList.Domain.Events.UserAccounts;

namespace ToDoList.Domain;

public class UserAccountEmailAddress : AggregateBase
{
    public override string Id { get => EmailAddress; }

    public string UserAccountId { get; protected set; }
    public string EmailAddress { get; protected set; }

    public DateTime? ConfirmedAt { get; protected set; }


    public UserAccountEmailAddress(IEnumerable<IEvent> events) : base(events)
    {
    }

    public UserAccountEmailAddress(string emailAddress) : base()
    {
        Apply(new UserAccountEmailRegistered(emailAddress));
    }

    public void ChangeEmailAddress(string newEmail)
    {
        Apply(new UserAccountEmailAddressChanged(newEmail));
    }

    public void ConfirmEmailAddress()
    {
        Apply(new UserAccountEmailAddressConfirmed());
    }

    public void AssignUserAccount(string userAccountId)
    {
        Apply(new UserAccountEmailAssigned(userAccountId, EmailAddress));
    }

    protected void On(UserAccountEmailRegistered @event)
    {
        EmailAddress = @event.EmailAddress;
        ConfirmedAt = null;
    }

    protected void On(UserAccountEmailAssigned @event)
    {
        UserAccountId = @event.UserAccountId;
    }

    protected void On(UserAccountEmailAddressChanged @event)
    {
        EmailAddress = @event.NewEmail;
        ConfirmedAt = null;
    }

    protected void On(UserAccountEmailAddressConfirmed _)
    {
        ConfirmedAt = DateTime.UtcNow;
    }

    protected override void RaiseEvent(IEvent @event)
    {
        ((dynamic)this).On((dynamic)@event);
    }
}
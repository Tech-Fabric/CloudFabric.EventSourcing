using CloudFabric.EventSourcing.Domain;
using CloudFabric.EventSourcing.EventStore;

using ToDoList.Domain.Events.UserAccounts;

namespace ToDoList.Domain;

public class UserAccountEmailAddress : AggregateBase
{
    public Guid UserAccountId { get; protected set; }
    public string EmailAddress { get; protected set; }

    public DateTime? ConfirmedAt { get; protected set; }

    public override string PartitionKey => Id.ToString();

    public override Guid Id
    {
        get => HashStringToGuid(EmailAddress);
    }

    public UserAccountEmailAddress(IEnumerable<IEvent> events) : base(events)
    {
    }

    public UserAccountEmailAddress(string emailAddress) : base()
    {
        Apply(new UserAccountEmailRegistered(emailAddress));
    }
    public void ChangeEmailAddress(string newEmail)
    {
        Apply(new UserAccountEmailAddressChanged(Id, UserAccountId, newEmail));
    }

    public void ConfirmEmailAddress()
    {
        Apply(new UserAccountEmailAddressConfirmed(Id, UserAccountId));
    }

    public void AssignUserAccount(Guid userAccountId)
    {
        Apply(new UserAccountEmailAssigned(Id, userAccountId, EmailAddress));
    }

    #region Event Handlers

    public void On(UserAccountEmailRegistered @event)
    {
        Id = @event.AggregateId;
        EmailAddress = @event.EmailAddress;
        ConfirmedAt = null;
    }

    public void On(UserAccountEmailAssigned @event)
    {
        UserAccountId = @event.UserAccountId;
    }

    public void On(UserAccountEmailAddressChanged @event)
    {
        EmailAddress = @event.NewEmail;
        ConfirmedAt = null;
    }

    public void On(UserAccountEmailAddressConfirmed _)
    {
        ConfirmedAt = DateTime.UtcNow;
    }

    #endregion
}
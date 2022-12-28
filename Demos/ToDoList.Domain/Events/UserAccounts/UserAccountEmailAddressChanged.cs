using CloudFabric.EventSourcing.EventStore;

namespace ToDoList.Domain.Events.UserAccounts;

public record UserAccountEmailAddressChanged : Event
{
    public UserAccountEmailAddressChanged() { }
    
    public UserAccountEmailAddressChanged(Guid userAccountId, string newEmail)
    {
        AggregateId = userAccountId;
        NewEmail = newEmail;
    }
    
    public string NewEmail { get; init; }
}
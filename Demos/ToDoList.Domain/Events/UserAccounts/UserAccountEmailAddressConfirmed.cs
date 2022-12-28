using CloudFabric.EventSourcing.EventStore;

namespace ToDoList.Domain.Events.UserAccounts;

public record UserAccountEmailAddressConfirmed : Event
{
    public UserAccountEmailAddressConfirmed() { }
    
    public UserAccountEmailAddressConfirmed(Guid userAccountId)
    {
        AggregateId = userAccountId;
    }
}

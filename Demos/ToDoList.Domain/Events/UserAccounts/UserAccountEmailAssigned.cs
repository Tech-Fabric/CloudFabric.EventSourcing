using CloudFabric.EventSourcing.EventStore;

namespace ToDoList.Domain.Events.UserAccounts;

public record UserAccountEmailAssigned : Event
{
    public UserAccountEmailAssigned() { }
    
    public UserAccountEmailAssigned(Guid userAccountId, string emailAddress)
    {
        AggregateId = userAccountId;
        EmailAddress = emailAddress;
    }
    
    public string EmailAddress { get; init; }
}
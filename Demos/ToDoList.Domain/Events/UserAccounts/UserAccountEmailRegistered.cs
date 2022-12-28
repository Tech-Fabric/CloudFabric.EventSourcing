using CloudFabric.EventSourcing.EventStore;

namespace ToDoList.Domain.Events.UserAccounts;

public record UserAccountEmailRegistered : Event
{
    public UserAccountEmailRegistered() { }
    
    public UserAccountEmailRegistered(Guid id, string emailAddress)
    {
        AggregateId = id;
        EmailAddress = emailAddress;
    }

    public string EmailAddress { get; init; }
}
using CloudFabric.EventSourcing.EventStore;

namespace ToDoList.Domain.Events.UserAccounts;

public record UserAccountRegistered : Event
{
    public UserAccountRegistered() { }
    
    public UserAccountRegistered(Guid id, string firstName, string hashedPassword)
    {
        AggregateId = id;
        FirstName = firstName;
        HashedPassword = hashedPassword;
    }

    public string FirstName { get; init; }
    
    public string HashedPassword { get; init; }
}
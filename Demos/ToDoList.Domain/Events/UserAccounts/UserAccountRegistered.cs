using CloudFabric.EventSourcing.EventStore;

namespace ToDoList.Domain.Events.UserAccounts;

public record UserAccountRegistered(
    Guid Id,
    string FirstName,
    string HashedPassword
) : Event(Id);
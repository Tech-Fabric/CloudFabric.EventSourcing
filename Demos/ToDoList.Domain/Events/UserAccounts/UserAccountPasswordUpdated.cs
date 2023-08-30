using CloudFabric.EventSourcing.EventStore;

namespace ToDoList.Domain.Events.UserAccounts;

public record UserAccountPasswordUpdated(
    Guid Id,
    string NewHashedPassword
) : Event(Id);
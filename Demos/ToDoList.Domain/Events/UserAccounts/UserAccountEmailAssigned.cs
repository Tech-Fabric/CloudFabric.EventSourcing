using CloudFabric.EventSourcing.EventStore;

namespace ToDoList.Domain.Events.UserAccounts;

public record UserAccountEmailAssigned(
    Guid UserAccountId,
    string EmailAddress
) : Event;
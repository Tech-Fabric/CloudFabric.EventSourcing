using CloudFabric.EventSourcing.EventStore;

namespace ToDoList.Domain.Events.UserAccounts;

public record UserAccountEmailAssigned(
    string UserAccountId,
    string EmailAddress
) : Event;
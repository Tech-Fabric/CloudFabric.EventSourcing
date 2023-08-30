using CloudFabric.EventSourcing.EventStore;

namespace ToDoList.Domain.Events.UserAccounts;

public record UserAccountEmailAssigned(
    Guid Id,
    Guid UserAccountId, 
    string EmailAddress
) : Event(Id);
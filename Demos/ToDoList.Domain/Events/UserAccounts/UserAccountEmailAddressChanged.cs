using CloudFabric.EventSourcing.EventStore;

namespace ToDoList.Domain.Events.UserAccounts;

public record UserAccountEmailAddressChanged(
    Guid Id,
    Guid UserAccountId, 
    string NewEmail
) : Event(Id);
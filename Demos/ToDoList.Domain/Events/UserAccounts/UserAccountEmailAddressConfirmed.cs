using CloudFabric.EventSourcing.EventStore;

namespace ToDoList.Domain.Events.UserAccounts;

public record UserAccountEmailAddressConfirmed(
    Guid Id,
    Guid UserAccountId
) : Event(Id);
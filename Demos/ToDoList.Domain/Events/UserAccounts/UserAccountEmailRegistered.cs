using CloudFabric.EventSourcing.EventStore;
using ToDoList.Domain.Utilities.Extensions;

namespace ToDoList.Domain.Events.UserAccounts;

public record UserAccountEmailRegistered(
    string EmailAddress
) : Event(EmailAddress.HashGuid());
using CloudFabric.EventSourcing.EventStore;

namespace ToDoList.Domain.Events.UserAccounts;

public record UserAccountRegistered(string Id, string FirstName, string HashedPassword) : Event;
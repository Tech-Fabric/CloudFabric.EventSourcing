using CloudFabric.EventSourcing.EventStore;

namespace ToDoList.Domain.Events.UserAccounts;

public record UserAccountPasswordUpdated(string NewHashedPassword) : Event;
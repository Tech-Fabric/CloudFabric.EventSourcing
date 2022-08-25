using CloudFabric.EventSourcing.EventStore;

namespace ToDoList.Domain.Events.TaskLists;

public record TaskCreated(string UserAccountId, string TaskListId, string Id, string Title, string? Description): Event;

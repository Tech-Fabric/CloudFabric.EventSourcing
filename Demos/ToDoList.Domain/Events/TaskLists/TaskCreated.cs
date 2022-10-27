using CloudFabric.EventSourcing.EventStore;

namespace ToDoList.Domain.Events.TaskLists;

public record TaskCreated(Guid UserAccountId, Guid TaskListId, Guid Id, string Title, string? Description): Event;

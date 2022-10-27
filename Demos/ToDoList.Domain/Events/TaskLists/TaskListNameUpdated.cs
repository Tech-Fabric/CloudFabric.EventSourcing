using CloudFabric.EventSourcing.EventStore;

namespace ToDoList.Domain.Events.TaskLists;

public record TaskListNameUpdated(Guid Id, string NewName): Event;

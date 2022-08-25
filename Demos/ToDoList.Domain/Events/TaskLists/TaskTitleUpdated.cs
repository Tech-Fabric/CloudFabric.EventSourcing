using CloudFabric.EventSourcing.EventStore;

namespace ToDoList.Domain.Events.TaskLists;

public record TaskTitleUpdated(string? TaskListId, string Id, string NewTitle): Event;

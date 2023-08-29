using CloudFabric.EventSourcing.EventStore;

namespace ToDoList.Domain.Events.TaskLists;

public record TaskCompletedStatusUpdpated(Guid TaskListId, Guid TaskId, bool IsCompleted) : Event;
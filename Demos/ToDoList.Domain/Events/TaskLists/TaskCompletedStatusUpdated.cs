using CloudFabric.EventSourcing.EventStore;

namespace ToDoList.Domain.Events.TaskLists;

public record TaskCompletedStatusUpdpated(string TaskListId, string TaskId, bool IsCompleted): Event;

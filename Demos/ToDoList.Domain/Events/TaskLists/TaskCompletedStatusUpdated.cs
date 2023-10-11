using CloudFabric.EventSourcing.EventStore;

namespace ToDoList.Domain.Events.TaskLists;

public record TaskCompletedStatusUpdpated(
    Guid TaskListId,
    Guid Id,
    bool IsCompleted)
: Event(Id);
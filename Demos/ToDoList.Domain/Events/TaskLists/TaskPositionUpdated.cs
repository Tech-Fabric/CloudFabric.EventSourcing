using CloudFabric.EventSourcing.EventStore;

namespace ToDoList.Domain.Events.TaskLists;

public record TaskPositionUpdated(
    Guid OldTaskListId, 
    Guid TaskListId, 
    Guid Id, 
    bool IsCompleted,
    double NewPosition
) : Event;
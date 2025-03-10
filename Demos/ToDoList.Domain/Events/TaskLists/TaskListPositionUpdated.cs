using CloudFabric.EventSourcing.EventStore;

namespace ToDoList.Domain.Events.TaskLists;

public record TaskListPositionUpdated(
    Guid Id,
    int NewPosition
) : Event;
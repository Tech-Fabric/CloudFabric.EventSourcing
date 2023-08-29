using CloudFabric.EventSourcing.EventStore;

namespace ToDoList.Domain.Events.TaskLists;

public record TaskTitleUpdated(
    Guid? TaskListId, 
    Guid Id, 
    string NewTitle
) : Event;
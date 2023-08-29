using CloudFabric.EventSourcing.EventStore;

namespace ToDoList.Domain.Events.TaskLists;

public record TaskListCreated(
    Guid UserAccountId,
    Guid Id,
    string Name
) : Event(Id);

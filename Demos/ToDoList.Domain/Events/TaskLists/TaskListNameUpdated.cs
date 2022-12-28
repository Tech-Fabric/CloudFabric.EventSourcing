using CloudFabric.EventSourcing.EventStore;

namespace ToDoList.Domain.Events.TaskLists;

public record TaskListNameUpdated : Event
{
    public TaskListNameUpdated() { }
    
    public TaskListNameUpdated(Guid id, string newName)
    {
        AggregateId = id;
        NewName = newName;
    }

    public string NewName { get; init; }
}

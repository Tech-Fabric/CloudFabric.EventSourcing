using CloudFabric.EventSourcing.EventStore;

namespace ToDoList.Domain.Events.TaskLists;

public record TaskTitleUpdated : Event
{
    public TaskTitleUpdated() { }
    
    public TaskTitleUpdated(Guid? taskListId, Guid id, string newTitle)
    {
        TaskListId = taskListId;
        AggregateId = id;
        NewTitle = newTitle;
    }

    public Guid? TaskListId { get; init; }
    public string NewTitle { get; init; }
}

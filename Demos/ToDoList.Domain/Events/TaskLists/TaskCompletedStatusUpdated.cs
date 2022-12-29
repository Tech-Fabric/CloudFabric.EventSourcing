using CloudFabric.EventSourcing.EventStore;

namespace ToDoList.Domain.Events.TaskLists;

public record TaskCompletedStatusUpdpated : Event
{
    public TaskCompletedStatusUpdpated() { }
    
    public TaskCompletedStatusUpdpated(Guid taskListId, Guid taskId, bool isCompleted)
    {
        TaskListId = taskListId;
        AggregateId = taskId;
        IsCompleted = isCompleted;
    }

    public Guid TaskListId { get; init; }
    
    public bool IsCompleted { get; init; }
}

using CloudFabric.EventSourcing.EventStore;

namespace ToDoList.Domain.Events.TaskLists;

public record TaskCreated : Event
{
    public TaskCreated() { }
    
    public TaskCreated(Guid userAccountId, Guid taskListId, Guid id, string title, string? description)
    {
        AggregateId = id;
        UserAccountId = userAccountId;
        TaskListId = taskListId;
        Title = title;
        Description = description;
    }
    
    public Guid UserAccountId { get; init; }
    
    public Guid TaskListId { get; init; }
    
    public string Title { get; init; }
    
    public string? Description { get; init; }
}

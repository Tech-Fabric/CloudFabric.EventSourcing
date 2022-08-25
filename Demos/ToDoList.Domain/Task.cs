using CloudFabric.EventSourcing.Domain;
using CloudFabric.EventSourcing.EventStore;
using ToDoList.Domain.Events.TaskLists;

namespace ToDoList.Domain;

public class Task : AggregateBase
{
    public string? TaskListId { get; protected set; }

    public string UserAccountId {get; protected set;}

    public string? Title { get; protected set; }

    public string? Description { get; protected set; }

    public bool IsCompleted { get; protected set; }

    public Task(IEnumerable<IEvent> events) : base(events)
    {
    }

    public Task(string userAccountId, string taskListId, string taskId, string title, string? description)
    {
        Apply(new TaskCreated(userAccountId, taskListId, taskId, title, description));
    }

    public void UpdateTitle(string newTitle)
    {
        Apply(new TaskTitleUpdated(TaskListId, Id, newTitle));
    }

    public void SetCompletedStatus(bool newCompletedStatus)
    {
        Apply(new TaskCompletedStatusUpdpated(TaskListId, Id, newCompletedStatus));
    }

    #region Event Handlers

    protected void On(TaskCreated @event)
    {
        Id = @event.Id;
        Title = @event.Title;
        Description = @event.Description;
    }

    protected void On(TaskTitleUpdated @event)
    {
        Title = @event.NewTitle;
    }

    protected void On(TaskCompletedStatusUpdpated @event)
    {
        IsCompleted = @event.IsCompleted;
    }

    protected override void RaiseEvent(IEvent @event)
    {
        ((dynamic)this).On((dynamic)@event);
    }

    #endregion
}

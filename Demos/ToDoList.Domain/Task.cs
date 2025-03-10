using CloudFabric.EventSourcing.Domain;
using CloudFabric.EventSourcing.EventStore;
using ToDoList.Domain.Events.TaskLists;

namespace ToDoList.Domain;

public class Task : AggregateBase
{
    public Guid UserAccountId { get; protected set; }

    public override string PartitionKey => UserAccountId.ToString();

    public Guid TaskListId { get; protected set; }

    public string? Title { get; protected set; }

    public string? Description { get; protected set; }

    public double Position { get; protected set; }

    public bool IsCompleted { get; protected set; }

    public Task(IEnumerable<IEvent> events) : base(events)
    {
    }

    public Task(Guid userAccountId, Guid taskListId, Guid taskId, string title, string? description)
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

    public void UpdatePosition(Guid newTaskListId, double newPosition)
    {
        Apply(new TaskPositionUpdated(TaskListId, newTaskListId, Id, IsCompleted, newPosition));
    }

    #region Event Handlers

    public void On(TaskCreated @event)
    {
        Id = @event.AggregateId;
        TaskListId = @event.TaskListId;
        UserAccountId = @event.UserAccountId;
        Title = @event.Title;
        Description = @event.Description;
    }

    public void On(TaskTitleUpdated @event)
    {
        Title = @event.NewTitle;
    }

    public void On(TaskCompletedStatusUpdpated @event)
    {
        IsCompleted = @event.IsCompleted;
    }

    public void On(TaskPositionUpdated @event)
    {
        TaskListId = @event.TaskListId;
        Position = @event.NewPosition;
    }

    #endregion
}
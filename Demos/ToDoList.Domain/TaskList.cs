using CloudFabric.EventSourcing.Domain;
using CloudFabric.EventSourcing.EventStore;
using ToDoList.Domain.Events.TaskLists;

namespace ToDoList.Domain;

public class TaskList : AggregateBase
{
    public string Name { get; protected set; }
    public string UserAccountId { get; protected set; }

    public TaskList(IEnumerable<IEvent> events) : base(events)
    {
    }

    public TaskList(string userAccountId, string id, string name)
    {
        Apply(new TaskListCreated(userAccountId, id, name));
    }

    public void UpdateName(string newName)
    {
        Apply(new TaskListNameUpdated(Id, newName));
    }

    #region Event Handlers

    protected void On(TaskListCreated @event)
    {
        Id = @event.Id;
        UserAccountId = @event.UserAccountId;
        Name = @event.Name;
    }

    protected void On(TaskListNameUpdated @event)
    {
        Name = @event.NewName;
    }

    protected override void RaiseEvent(IEvent @event)
    {
        ((dynamic)this).On((dynamic)@event);
    }

    #endregion
}

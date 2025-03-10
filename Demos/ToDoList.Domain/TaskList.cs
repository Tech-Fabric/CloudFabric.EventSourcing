using CloudFabric.EventSourcing.Domain;
using CloudFabric.EventSourcing.EventStore;
using ToDoList.Domain.Events.TaskLists;

namespace ToDoList.Domain;

public class TaskList : AggregateBase
{
    public string Name { get; protected set; }
    
    public Guid UserAccountId { get; protected set; }

    public override string PartitionKey => UserAccountId.ToString();

    public double Position { get; set; }

    public TaskList(IEnumerable<IEvent> events) : base(events)
    {
    }

    public TaskList(Guid userAccountId, Guid id, string name)
    {
        Apply(new TaskListCreated(userAccountId, id, name));
    }

    public void UpdateName(string newName)
    {
        Apply(new TaskListNameUpdated(Id, newName));
    }

    public void UpdatePosition(int newPosition)
    {
        Apply(new TaskListPositionUpdated(Id, newPosition));
    }

    #region Event Handlers

    public void On(TaskListCreated @event)
    {
        Id = @event.AggregateId;
        UserAccountId = @event.UserAccountId;
        Name = @event.Name;
    }

    public void On(TaskListNameUpdated @event)
    {
        Name = @event.NewName;
    }
    
    public void On(TaskListPositionUpdated @event)
    {
        Position = @event.NewPosition;
    }
    
    #endregion
}
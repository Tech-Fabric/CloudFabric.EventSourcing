using CloudFabric.EventSourcing.Domain;
using CloudFabric.EventSourcing.EventStore;
using ToDoList.Domain.Events.TaskLists;

namespace ToDoList.Domain;

public class TaskList : AggregateBase
{
    public string Name { get; protected set; }
    public string UserAccountId { get; protected set; }

    public override string PartitionKey => PartitionKeys.GetTaskListPartitionKey();

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

    public void On(TaskListCreated @event)
    {
        Id = @event.Id;
        UserAccountId = @event.UserAccountId;
        Name = @event.Name;
    }

    public void On(TaskListNameUpdated @event)
    {
        Name = @event.NewName;
    }

    #endregion
}

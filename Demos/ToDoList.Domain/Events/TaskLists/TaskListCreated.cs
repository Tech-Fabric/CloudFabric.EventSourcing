using CloudFabric.EventSourcing.EventStore;

namespace ToDoList.Domain.Events.TaskLists;

public record TaskListCreated : Event
{
    public TaskListCreated(Guid userAccountId, Guid id, string name)
    {
        UserAccountId = userAccountId;
        AggregateId = id;
        Name = name;
    }

    public Guid UserAccountId { get; init; }

    public string Name { get; init; }
}

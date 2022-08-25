using CloudFabric.EventSourcing.EventStore;

namespace ToDoList.Domain.Events.TaskLists;

public record TaskListCreated(string UserAccountId, string Id, string Name): Event;

using CloudFabric.Projections;
using ToDoList.Domain.Events.TaskLists;

namespace ToDoList.Domain.Projections.TaskLists;


public class TaskListsProjectionBuilder : ProjectionBuilder<TaskListProjectionItem>,
    IHandleEvent<TaskListCreated>,
    IHandleEvent<TaskListNameUpdated>,
    IHandleEvent<TaskCreated>,
    IHandleEvent<TaskCompletedStatusUpdpated>
{
    public TaskListsProjectionBuilder(ProjectionRepositoryFactory projectionRepositoryFactory) : base(projectionRepositoryFactory)
    {
    }

    public async System.Threading.Tasks.Task On(TaskListCreated @event)
    {
        await UpsertDocument(new TaskListProjectionItem() {
            Id = @event.Id,
            UserAccountId = @event.UserAccountId,
            Name = @event.Name,
            UpdatedAt = @event.Timestamp,
            TasksCount = 0,
            ClosedTasksCount = 0,
            OpenTasksCount = 0
        }, @event.PartitionKey);
    }

    public async System.Threading.Tasks.Task On(TaskListNameUpdated @event)
    {
        await UpdateDocument(
            @event.Id,
            @event.PartitionKey,
            (projectionDocument) => 
            {
                projectionDocument.Name = @event.NewName;
                projectionDocument.UpdatedAt = @event.Timestamp;
            }
        );
    }

    public async System.Threading.Tasks.Task On(TaskCreated @event)
    {
        await UpdateDocument(
            @event.TaskListId,
            @event.PartitionKey,
            (projectionDocument) =>
            {
                projectionDocument.TasksCount += 1;
            }
        );
    }

    public async System.Threading.Tasks.Task On(TaskCompletedStatusUpdpated @event)
    {
        await UpdateDocument(
            @event.TaskListId,
            @event.PartitionKey,
            (projectionDocument) => 
            {
                projectionDocument.OpenTasksCount += @event.IsCompleted ? -1 : 1;
                projectionDocument.ClosedTasksCount += @event.IsCompleted ? 1 : -1;
            }
        );
    }
}

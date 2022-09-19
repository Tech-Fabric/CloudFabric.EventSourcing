using CloudFabric.Projections;
using ToDoList.Domain.Events.TaskLists;

namespace ToDoList.Domain.Projections.TaskLists;


public class TaskListsProjectionBuilder : ProjectionBuilder<TaskListProjectionItem>,
    IHandleEvent<TaskListCreated>,
    IHandleEvent<TaskListNameUpdated>,
    IHandleEvent<TaskCreated>,
    IHandleEvent<TaskCompletedStatusUpdpated>
{
    public TaskListsProjectionBuilder(IProjectionRepository<TaskListProjectionItem> repository) : base(repository)
    {
    }

    public async System.Threading.Tasks.Task On(TaskListCreated @event, string partitionKey)
    {
        await UpsertDocument(new TaskListProjectionItem() {
            Id = @event.Id,
            UserAccountId = @event.UserAccountId,
            Name = @event.Name,
            UpdatedAt = @event.Timestamp,
            TasksCount = 0,
            ClosedTasksCount = 0,
            OpenTasksCount = 0
        }, partitionKey);
    }

    public async System.Threading.Tasks.Task On(TaskListNameUpdated @event, string partitionKey)
    {
        await UpdateDocument(
            @event.Id,
            partitionKey,
            (projectionDocument) => 
            {
                projectionDocument.Name = @event.NewName;
                projectionDocument.UpdatedAt = @event.Timestamp;
            }
        );
    }

    public async System.Threading.Tasks.Task On(TaskCreated @event, string partitionKey)
    {
        await UpdateDocument(
            @event.TaskListId,
            partitionKey,
            (projectionDocument) =>
            {
                projectionDocument.TasksCount += 1;
            }
        );
    }

    public async System.Threading.Tasks.Task On(TaskCompletedStatusUpdpated @event, string partitionKey)
    {
        await UpdateDocument(
            @event.TaskListId,
            partitionKey,
            (projectionDocument) => 
            {
                projectionDocument.OpenTasksCount += @event.IsCompleted ? -1 : 1;
                projectionDocument.ClosedTasksCount += @event.IsCompleted ? 1 : -1;
            }
        );
    }
}

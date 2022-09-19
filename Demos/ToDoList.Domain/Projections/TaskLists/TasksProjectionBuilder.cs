using CloudFabric.Projections;
using ToDoList.Domain.Events.TaskLists;

namespace ToDoList.Domain.Projections.TaskLists;


public class TasksProjectionBuilder : ProjectionBuilder<TaskProjectionItem>,
    IHandleEvent<TaskCreated>,
    IHandleEvent<TaskTitleUpdated>,
    IHandleEvent<TaskCompletedStatusUpdpated>
{
    public TasksProjectionBuilder(IProjectionRepository<TaskProjectionItem> repository) : base(repository)
    {
    }

    public async System.Threading.Tasks.Task On(TaskCreated @event, string partitionKey)
    {
        await UpsertDocument(new TaskProjectionItem() {
            Id = @event.Id,
            Title = @event.Title,
            Description = @event.Description,
            UserAccountId = @event.UserAccountId,
            TaskListId = @event.TaskListId,
            IsCompleted = false
        }, partitionKey);
    }

    public async System.Threading.Tasks.Task On(TaskCompletedStatusUpdpated @event, string partitionKey)
    {
        await UpdateDocument(
            @event.TaskId,
            partitionKey,
            (projectionDocument) =>
            {
                projectionDocument.IsCompleted = @event.IsCompleted;
            }
        );
    }

    public async System.Threading.Tasks.Task On(TaskTitleUpdated @event, string partitionKey)
    {
        await UpdateDocument(@event.Id,
            partitionKey,
            (projectionDocument) => 
            {
                projectionDocument.Title = @event.NewTitle;
            }
        );
    }
}

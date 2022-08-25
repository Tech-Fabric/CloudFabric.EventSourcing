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

    public async System.Threading.Tasks.Task On(TaskCreated @event)
    {
        await UpsertDocument(new TaskProjectionItem() {
            Id = @event.Id,
            Title = @event.Title,
            Description = @event.Description,
            UserAccountId = @event.UserAccountId,
            TaskListId = @event.TaskListId,
            IsCompleted = false
        });
    }

    public async System.Threading.Tasks.Task On(TaskCompletedStatusUpdpated @event)
    {
        await UpdateDocument(@event.TaskId, (projectionDocument) => {
            projectionDocument.IsCompleted = @event.IsCompleted;
        });
    }

    public async System.Threading.Tasks.Task On(TaskTitleUpdated @event)
    {
        await UpdateDocument(@event.Id, (projectionDocument) => {
            projectionDocument.Title = @event.NewTitle;
        });
    }
}

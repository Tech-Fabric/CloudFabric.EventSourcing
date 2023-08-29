using CloudFabric.Projections;
using ToDoList.Domain.Events.TaskLists;

namespace ToDoList.Domain.Projections.TaskLists;


public class TasksProjectionBuilder : ProjectionBuilder<TaskProjectionItem>,
    IHandleEvent<TaskCreated>,
    IHandleEvent<TaskTitleUpdated>,
    IHandleEvent<TaskCompletedStatusUpdpated>
{
    public TasksProjectionBuilder(
        ProjectionRepositoryFactory projectionRepositoryFactory, 
        ProjectionOperationIndexSelector indexSelector
    ) : base(projectionRepositoryFactory, indexSelector)
    {
    }

    public async System.Threading.Tasks.Task On(TaskCreated @event)
    {
        await UpsertDocument(
            new TaskProjectionItem() 
            {
                Id = @event.AggregateId,
                Title = @event.Title,
                Description = @event.Description,
                UserAccountId = @event.UserAccountId,
                TaskListId = @event.TaskListId,
                IsCompleted = false
            },
            @event.PartitionKey,
            @event.Timestamp
        );
    }

    public async System.Threading.Tasks.Task On(TaskCompletedStatusUpdpated @event)
    {
        await UpdateDocument(
            @event.AggregateId,
            @event.PartitionKey,
            @event.Timestamp,
            (projectionDocument) =>
            {
                projectionDocument.IsCompleted = @event.IsCompleted;
            }
        );
    }

    public async System.Threading.Tasks.Task On(TaskTitleUpdated @event)
    {
        await UpdateDocument(
            @event.AggregateId,
            @event.PartitionKey,
            @event.Timestamp,
            (projectionDocument) => 
            {
                projectionDocument.Title = @event.NewTitle;
            }
        );
    }
    
    public async System.Threading.Tasks.Task On(AggregateUpdatedEvent<Task> @event)
    {
        await SetDocumentUpdatedAt(@event.AggregateId, @event.PartitionKey, @event.UpdatedAt);
    }
}
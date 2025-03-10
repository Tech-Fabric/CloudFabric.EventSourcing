using CloudFabric.Projections;
using ToDoList.Domain.Events.TaskLists;

namespace ToDoList.Domain.Projections.TaskLists;


public class TasksProjectionBuilder : ProjectionBuilder<TaskProjectionItem>,
    IHandleEvent<TaskCreated>,
    IHandleEvent<TaskTitleUpdated>,
    IHandleEvent<TaskCompletedStatusUpdpated>,
    IHandleEvent<TaskPositionUpdated>
{
    public TasksProjectionBuilder(
        ProjectionRepositoryFactory projectionRepositoryFactory, 
        ProjectionOperationIndexSelector indexSelector
    ) : base(projectionRepositoryFactory, indexSelector)
    {
    }

    public async System.Threading.Tasks.Task On(TaskCreated evt)
    {
        await UpsertDocument(
            new TaskProjectionItem() 
            {
                Id = evt.AggregateId,
                Title = evt.Title,
                Description = evt.Description,
                UserAccountId = evt.UserAccountId,
                TaskListId = evt.TaskListId,
                IsCompleted = false
            },
            evt.PartitionKey,
            evt.Timestamp
        );
    }

    public async System.Threading.Tasks.Task On(TaskCompletedStatusUpdpated evt)
    {
        await UpdateDocument(
            evt.AggregateId,
            evt.PartitionKey,
            evt.Timestamp,
            (projectionDocument) =>
            {
                projectionDocument.IsCompleted = evt.IsCompleted;
            }
        );
    }

    public async System.Threading.Tasks.Task On(TaskTitleUpdated evt)
    {
        await UpdateDocument(
            evt.AggregateId,
            evt.PartitionKey,
            evt.Timestamp,
            (projectionDocument) => 
            {
                projectionDocument.Title = evt.NewTitle;
            }
        );
    }
    
    public async System.Threading.Tasks.Task On(TaskPositionUpdated evt)
    {
        await UpdateDocument(
            evt.AggregateId,
            evt.PartitionKey,
            evt.Timestamp,
            (projectionDocument) =>
            {
                projectionDocument.TaskListId = evt.TaskListId;
                projectionDocument.Position = evt.NewPosition;
            }
        );
    }
    
    public async System.Threading.Tasks.Task On(AggregateUpdatedEvent<Task> @event)
    {
        await SetDocumentUpdatedAt(@event.AggregateId, @event.PartitionKey, @event.UpdatedAt);
    }
}
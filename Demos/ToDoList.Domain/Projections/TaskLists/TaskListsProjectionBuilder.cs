using CloudFabric.Projections;
using ToDoList.Domain.Events.TaskLists;

namespace ToDoList.Domain.Projections.TaskLists;


public class TaskListsProjectionBuilder : ProjectionBuilder<TaskListProjectionItem>,
    IHandleEvent<TaskListCreated>,
    IHandleEvent<TaskListNameUpdated>,
    IHandleEvent<TaskListPositionUpdated>,
    IHandleEvent<TaskCreated>,
    IHandleEvent<TaskCompletedStatusUpdpated>,
    IHandleEvent<TaskPositionUpdated>
{
    public TaskListsProjectionBuilder(
        ProjectionRepositoryFactory projectionRepositoryFactory, 
        ProjectionOperationIndexSelector indexSelector
    ) : base(projectionRepositoryFactory, indexSelector)
    {
    }

    public async System.Threading.Tasks.Task On(TaskListCreated evt)
    {
        await UpsertDocument(
            new TaskListProjectionItem() 
            {
                Id = evt.AggregateId,
                UserAccountId = evt.UserAccountId,
                Name = evt.Name,
                CreatedAt = evt.Timestamp,
                UpdatedAt = evt.Timestamp,
                TasksCount = 0,
                ClosedTasksCount = 0,
                OpenTasksCount = 0
            },
            evt.PartitionKey,
            evt.Timestamp
        );
    }

    public async System.Threading.Tasks.Task On(TaskListNameUpdated evt)
    {
        await UpdateDocument(
            evt.AggregateId,
            evt.PartitionKey,
            evt.Timestamp,
            (projectionDocument) => 
            {
                projectionDocument.Name = evt.NewName;
                projectionDocument.UpdatedAt = evt.Timestamp;
            }
        );
    }
    
    public async System.Threading.Tasks.Task On(TaskListPositionUpdated evt)
    {
        await UpdateDocument(
            evt.AggregateId,
            evt.PartitionKey,
            evt.Timestamp,
            (projectionDocument) => 
            {
                projectionDocument.Position = evt.NewPosition;
                projectionDocument.UpdatedAt = evt.Timestamp;
            }
        );
    }

    public async System.Threading.Tasks.Task On(TaskCreated evt)
    {
        await UpdateDocument(
            evt.TaskListId,
            evt.PartitionKey,
            evt.Timestamp,
            (projectionDocument) =>
            {
                projectionDocument.TasksCount += 1;
            }
        );
    }

    public async System.Threading.Tasks.Task On(TaskCompletedStatusUpdpated evt)
    {
        await UpdateDocument(
            evt.TaskListId,
            evt.PartitionKey,
            evt.Timestamp,
            (projectionDocument) => 
            {
                projectionDocument.OpenTasksCount += evt.IsCompleted ? -1 : 1;
                projectionDocument.ClosedTasksCount += evt.IsCompleted ? 1 : -1;
            }
        );
    }
    
    public async System.Threading.Tasks.Task On(TaskPositionUpdated evt)
    {
        await UpdateDocument(
            evt.OldTaskListId,
            evt.PartitionKey,
            evt.Timestamp,
            (projectionDocument) => 
            {
                if (evt.IsCompleted)
                {
                    projectionDocument.ClosedTasksCount -= 1;
                }
                else
                {
                    projectionDocument.OpenTasksCount -= 1;
                }
            }
        );
        
        await UpdateDocument(
            evt.TaskListId,
            evt.PartitionKey,
            evt.Timestamp,
            (projectionDocument) => 
            {
                if (evt.IsCompleted)
                {
                    projectionDocument.ClosedTasksCount += 1;
                }
                else
                {
                    projectionDocument.OpenTasksCount += 1;
                }
            }
        );
    }
    
    public async System.Threading.Tasks.Task On(AggregateUpdatedEvent<TaskList> @event)
    {
        await SetDocumentUpdatedAt(@event.AggregateId, @event.PartitionKey, @event.UpdatedAt);
    }
}
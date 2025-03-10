using ToDoList.Models;
using ToDoList.Models.RequestModels.TaskLists;
using ToDoList.Models.ViewModels.TaskLists;

namespace ToDoList.Services.Interfaces;

public interface ITaskListsService
{
    Task<ServiceResult<TaskViewModel>> CreateTask(CreateTaskRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<TaskViewModel>> UpdateTaskPosition(UpdateTaskPositionRequest request, CancellationToken cancellationToken);
    
    Task<ServiceResult<TaskListWithTasksViewModel>> CreateTaskList(CreateTaskListRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<TaskListViewModel>> GetTaskListById(Guid taskListId, CancellationToken cancellationToken);

    Task<ServiceResult<List<TaskListViewModel>>> GetTaskLists(
        string? search = null,
        int limit = 100,
        int offset = 0,
        CancellationToken cancellationToken = default
    );

    Task<ServiceResult<List<TaskListWithTasksViewModel>>> GetTaskListsWithTasks(
        string? search = null,
        int limit = 100,
        int offset = 0,
        CancellationToken cancellationToken = default
    );

    Task<ServiceResult<Dictionary<Guid, List<TaskViewModel>>>> GetTasks(
        string taskListIds,
        CancellationToken cancellationToken
    );
    
    Task<ServiceResult<List<TaskViewModel>>> GetTasks(
        Guid taskListId,
        string search,
        int limit, 
        int offset,
        CancellationToken cancellationToken);
    
    Task<ServiceResult<TaskListViewModel>> UpdateTaskListName(Guid taskListId, UpdateTaskListNameRequest request, CancellationToken cancellationToken);
}
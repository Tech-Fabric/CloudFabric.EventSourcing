using ToDoList.Models;
using ToDoList.Models.RequestModels.TaskLists;
using ToDoList.Models.ViewModels.TaskLists;

namespace ToDoList.Services.Interfaces;

public interface ITaskListsService
{
    Task<ServiceResult<TaskViewModel>> CreateTask(CreateTaskRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<TaskListViewModel>> CreateTaskList(CreateTaskListRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<TaskListViewModel>> GetTaskListById(string taskListId, CancellationToken cancellationToken);

    Task<ServiceResult<List<TaskViewModel>>> GetTaskLists(
        string search,
        int limit, 
        int offset,
        CancellationToken cancellationToken
    );

    Task<ServiceResult<List<TaskViewModel>>> GetTasks(
        string taskListId,
        string search,
        int limit, 
        int offset,
        CancellationToken cancellationToken);
    Task<ServiceResult<TaskListViewModel>> UpdateTaskListName(string taskListId, UpdateTaskListNameRequest request, CancellationToken cancellationToken);
}

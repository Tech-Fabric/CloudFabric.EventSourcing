using ToDoList.Models;
using ToDoList.Models.RequestModels.TaskLists;
using ToDoList.Models.ViewModels.TaskLists;

namespace ToDoList.Ui.Services;

public class TaskListsService
{
    private readonly IServiceCommunicationProvider _serviceCommunicationProvider;

    public TaskListsService(IServiceCommunicationProvider serviceCommunicationProvider){
        _serviceCommunicationProvider = serviceCommunicationProvider;
    }

    public async Task<ServiceResult<List<TaskListViewModel>>> GetTaskLists(int limit, int offset)
    {
        return await _serviceCommunicationProvider.Get<List<TaskListViewModel>>(
            $"task_list?limit={limit}&offset={offset}"
        );
    }
    
    public async Task<ServiceResult<Dictionary<Guid, List<TaskViewModel>>>> GetTasks(List<Guid> taskListIds)
    {
        return await _serviceCommunicationProvider.Get<Dictionary<Guid, List<TaskViewModel>>>(
            $"tasks?task_list_ids={string.Join(',', taskListIds)}"
        );
    }

    public async Task<ServiceResult<TaskListViewModel>> CreateTaskList(CreateTaskListRequest request)
    {
        return await _serviceCommunicationProvider.SendCommand<TaskListViewModel>(
            $"task_list",
            request
        );
    }
    
    public async Task<ServiceResult<TaskViewModel>> CreateTask(CreateTaskRequest request)
    {
        return await _serviceCommunicationProvider.SendCommand<TaskViewModel>(
            $"task_list/{request.TaskListId}/tasks",
            request
        );
    }
}
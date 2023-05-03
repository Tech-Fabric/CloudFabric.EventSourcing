using AutoMapper;

using CloudFabric.EventSourcing.Domain;
using CloudFabric.EventSourcing.EventStore.Persistence;
using CloudFabric.Projections;
using CloudFabric.Projections.Queries;

using ToDoList.Domain;
using ToDoList.Domain.Projections.TaskLists;
using ToDoList.Models;
using ToDoList.Models.RequestModels.TaskLists;
using ToDoList.Models.ViewModels.TaskLists;
using ToDoList.Services.Interfaces;

namespace ToDoList.Services.Implementations;

public class TaskListsService : ITaskListsService
{
    private readonly IMapper _mapper;
    private readonly EventUserInfo _userInfo;

    private readonly AggregateRepository<TaskList> _taskListsRepository;
    private readonly AggregateRepository<Domain.Task> _tasksRepository;

    private readonly IProjectionRepository<TaskListProjectionItem> _taskListsProjectionRepository;
    private readonly IProjectionRepository<TaskProjectionItem> _tasksProjectionRepository;

    public TaskListsService(
        IMapper mapper,
        EventUserInfo userInfo,
        AggregateRepository<TaskList> taskListsRepository,
        AggregateRepository<Domain.Task> tasksRepository,
        ProjectionRepositoryFactory projectionRepositoryFactory 
    )
    {
        _mapper = mapper;
        _userInfo = userInfo;
        _taskListsRepository = taskListsRepository;
        _tasksRepository = tasksRepository;
        _taskListsProjectionRepository = projectionRepositoryFactory.GetProjectionRepository<TaskListProjectionItem>();
        _tasksProjectionRepository = projectionRepositoryFactory.GetProjectionRepository<TaskProjectionItem>();
    }

    public async System.Threading.Tasks.Task<ServiceResult<TaskListViewModel>> CreateTaskList(CreateTaskListRequest request, CancellationToken cancellationToken)
    {
        var validationProblemDetails = ValidationHelper.Validate(request);

        if (validationProblemDetails != null)
        {
            return ServiceResult<TaskListViewModel>.Failed(validationProblemDetails);
        }

        var taskList = new TaskList(_userInfo.UserId, Guid.NewGuid(), request.Name);

        await _taskListsRepository.SaveAsync(_userInfo, taskList, cancellationToken);

        return ServiceResult<TaskListViewModel>.Success(_mapper.Map<TaskListViewModel>(taskList));
    }

    public async System.Threading.Tasks.Task<ServiceResult<TaskListViewModel>> GetTaskListById(Guid taskListId, CancellationToken cancellationToken)
    {
        var taskList = await _taskListsRepository.LoadAsync(taskListId, PartitionKeys.GetTaskListPartitionKey(), cancellationToken);

        if (taskList == null)
        {
            return ServiceResult<TaskListViewModel>.Failed("task_list_not_found", "Task list does not exist");
        }

        return ServiceResult<TaskListViewModel>.Success(_mapper.Map<TaskListViewModel>(taskList));
    }

    public async System.Threading.Tasks.Task<ServiceResult<TaskListViewModel>> UpdateTaskListName(Guid taskListId, UpdateTaskListNameRequest request, CancellationToken cancellationToken)
    {
        var validationProblemDetails = ValidationHelper.Validate(request);

        if (validationProblemDetails != null)
        {
            return ServiceResult<TaskListViewModel>.Failed(validationProblemDetails);
        }

        var taskList = await _taskListsRepository.LoadAsync(taskListId, PartitionKeys.GetTaskListPartitionKey(), cancellationToken);

        if (taskList == null)
        {
            return ServiceResult<TaskListViewModel>.Failed("task_list_not_found", "Task list does not exist");
        }

        taskList.UpdateName(request.Name);

        await _taskListsRepository.SaveAsync(_userInfo, taskList, cancellationToken);

        return ServiceResult<TaskListViewModel>.Success(_mapper.Map<TaskListViewModel>(taskList));
    }

    public async System.Threading.Tasks.Task<ServiceResult<TaskViewModel>> CreateTask(CreateTaskRequest request, CancellationToken cancellationToken)
    {
        var validationProblemDetails = ValidationHelper.Validate(request);

        if (validationProblemDetails != null)
        {
            return ServiceResult<TaskViewModel>.Failed(validationProblemDetails);
        }

        var task = new Domain.Task(_userInfo.UserId, request.TaskListId, Guid.NewGuid(), request.Name, request.Description);

        await _tasksRepository.SaveAsync(_userInfo, task, cancellationToken);

        return ServiceResult<TaskViewModel>.Success(_mapper.Map<TaskViewModel>(task));
    }

    public async System.Threading.Tasks.Task<ServiceResult<List<TaskViewModel>>> GetTasks(
        Guid taskListId,
        string search,
        int limit,
        int offset,
        CancellationToken cancellationToken
    )
    {
        var projectionQuery = ProjectionQueryExpressionExtensions.Where<TaskProjectionItem>(
            t => t.UserAccountId == _userInfo.UserId && t.TaskListId == taskListId
        );
        projectionQuery.SearchText = search;
        projectionQuery.Limit = limit;
        projectionQuery.Offset = offset;

        var tasks = await _tasksProjectionRepository.Query(
            projectionQuery,
            PartitionKeys.GetTaskPartitionKey(),
            cancellationToken
        );

        return ServiceResult<List<TaskViewModel>>.Success(_mapper.Map<List<TaskViewModel>>(tasks));
    }

    public async System.Threading.Tasks.Task<ServiceResult<List<TaskListViewModel>>> GetTaskLists(
        string search,
        int limit, 
        int offset,
        CancellationToken cancellationToken
    )
    {
        var projectionQuery = ProjectionQueryExpressionExtensions.Where<TaskListProjectionItem>(t => t.UserAccountId == _userInfo.UserId);
        projectionQuery.SearchText = search;
        projectionQuery.Limit = limit;
        projectionQuery.Offset = offset;

        var taskLists = await _taskListsProjectionRepository.Query(
            projectionQuery,
            PartitionKeys.GetTaskListPartitionKey(),
            cancellationToken
        );

        return ServiceResult<List<TaskListViewModel>>.Success(_mapper.Map<List<TaskListViewModel>>(taskLists));
    }
}

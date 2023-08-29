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

    public async Task<ServiceResult<TaskListViewModel>> CreateTaskList(CreateTaskListRequest request, CancellationToken cancellationToken)
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

    public async Task<ServiceResult<TaskListViewModel>> GetTaskListById(Guid taskListId, CancellationToken cancellationToken)
    {
        var taskList = await _taskListsRepository.LoadAsync(taskListId, PartitionKeys.GetTaskListPartitionKey(), cancellationToken);

        if (taskList == null)
        {
            return ServiceResult<TaskListViewModel>.Failed("task_list_not_found", "Task list does not exist");
        }

        return ServiceResult<TaskListViewModel>.Success(_mapper.Map<TaskListViewModel>(taskList));
    }

    public async Task<ServiceResult<List<TaskListViewModel>>> GetTaskLists(
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

        return ServiceResult<List<TaskListViewModel>>.Success(
            _mapper.Map<List<TaskListViewModel>>(taskLists.Records.Select(r => r.Document))
        );
    }
    
    public async Task<ServiceResult<TaskListViewModel>> UpdateTaskListName(
        Guid taskListId, UpdateTaskListNameRequest request, CancellationToken cancellationToken
    )
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

    public async Task<ServiceResult<TaskViewModel>> CreateTask(CreateTaskRequest request, CancellationToken cancellationToken)
    {
        var validationProblemDetails = ValidationHelper.Validate(request);

        if (validationProblemDetails != null)
        {
            return ServiceResult<TaskViewModel>.Failed(validationProblemDetails);
        }

        var taskList = await _taskListsRepository.LoadAsync(request.TaskListId.GetValueOrDefault(), PartitionKeys.GetTaskListPartitionKey(), cancellationToken);

        if (taskList == null)
        {
            return ServiceResult<TaskViewModel>.ValidationFailedOneParam(nameof(request.TaskListId), "Task list with given id was not found");
        }

        var task = new Domain.Task(_userInfo.UserId, request.TaskListId.GetValueOrDefault(), Guid.NewGuid(), request.Name, request.Description);

        await _tasksRepository.SaveAsync(_userInfo, task, cancellationToken);

        return ServiceResult<TaskViewModel>.Success(_mapper.Map<TaskViewModel>(task));
    }

    public async Task<ServiceResult<List<TaskViewModel>>> GetTasks(
        Guid taskListId,
        string search,
        int limit,
        int offset,
        CancellationToken cancellationToken
    ) {
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
    
    public async Task<ServiceResult<Dictionary<Guid, List<TaskViewModel>>>> GetTasks(
        string? taskListIds,
        CancellationToken cancellationToken
    )
    {
        var taskListIdsList = new List<Guid>();

        var projectionQuery = ProjectionQueryExpressionExtensions.Where<TaskProjectionItem>(
            t => t.UserAccountId == _userInfo.UserId
        );

        if (!string.IsNullOrEmpty(taskListIds))
        {
            var taskListIdsFilter = new Filter();
            
            foreach (var id in taskListIds.Split(",").Select(id => id.Trim()))
            {
                var guid = Guid.Empty;
                if (Guid.TryParse(id, out guid))
                {
                    taskListIdsList.Add(guid);
                    taskListIdsFilter.Or(nameof(TaskProjectionItem.TaskListId), FilterOperator.Equal, guid);
                }
                else
                {
                    return ServiceResult<Dictionary<Guid, List<TaskViewModel>>>.ValidationFailedOneParam("task_list_ids", "Provided guid is not valid");
                }
            }
            
            projectionQuery.Filters.Add(taskListIdsFilter);
        }
        
        var tasks = await _tasksProjectionRepository.Query(
            projectionQuery,
            PartitionKeys.GetTaskPartitionKey(),
            cancellationToken
        );

        var result = new Dictionary<Guid, List<TaskViewModel>>();

        foreach (var task in tasks.Records)
        {
            if (!result.ContainsKey(task.Document.TaskListId))
            {
                result.Add(task.Document.TaskListId, new List<TaskViewModel>());
            }
            
            result[task.Document.TaskListId].Add(_mapper.Map<TaskViewModel>(task.Document));
        }

        return ServiceResult<Dictionary<Guid, List<TaskViewModel>>>.Success(result);
    }

    
}
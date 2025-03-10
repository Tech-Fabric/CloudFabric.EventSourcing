using System.Diagnostics;
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

    private readonly ActivitySource _activitySource;
    
    public TaskListsService(
        IMapper mapper,
        EventUserInfo userInfo,
        AggregateRepository<TaskList> taskListsRepository,
        AggregateRepository<Domain.Task> tasksRepository,
        ProjectionRepositoryFactory projectionRepositoryFactory,
        ActivitySource activitySource
    )
    {
        _mapper = mapper;
        _userInfo = userInfo;
        _taskListsRepository = taskListsRepository;
        _tasksRepository = tasksRepository;
        _taskListsProjectionRepository = projectionRepositoryFactory.GetProjectionRepository<TaskListProjectionItem>();
        _tasksProjectionRepository = projectionRepositoryFactory.GetProjectionRepository<TaskProjectionItem>();

        _activitySource = activitySource;
    }

    public async Task<ServiceResult<TaskViewModel>> UpdateTaskPosition(UpdateTaskPositionRequest request, CancellationToken cancellationToken)
    {
        var task = await _tasksRepository.LoadAsync(request.TaskId, _userInfo.UserId.ToString(), cancellationToken);

        if (task == null)
        {
            return ServiceResult<TaskViewModel>.Failed("task_list_not_found", "Task list does not exist");
        }

        task.UpdatePosition(request.NewTaskListId, request.NewPosition);
        
        await _tasksRepository.SaveAsync(_userInfo, task, cancellationToken);

        return ServiceResult<TaskViewModel>.Success(_mapper.Map<TaskViewModel>(task));
    }

    public async Task<ServiceResult<TaskListWithTasksViewModel>> CreateTaskList(CreateTaskListRequest request, CancellationToken cancellationToken)
    {
        var validationProblemDetails = ValidationHelper.Validate(request);

        if (validationProblemDetails != null)
        {
            return ServiceResult<TaskListWithTasksViewModel>.Failed(validationProblemDetails);
        }

        var taskList = new TaskList(_userInfo.UserId, Guid.NewGuid(), request.Name);

        await _taskListsRepository.SaveAsync(_userInfo, taskList, cancellationToken);

        return ServiceResult<TaskListWithTasksViewModel>.Success(_mapper.Map<TaskListWithTasksViewModel>(taskList));
    }

    public async Task<ServiceResult<TaskListViewModel>> GetTaskListById(Guid taskListId, CancellationToken cancellationToken)
    {
        var taskList = await _taskListsRepository.LoadAsync(taskListId, _userInfo.UserId.ToString(), cancellationToken);

        if (taskList == null)
        {
            return ServiceResult<TaskListViewModel>.Failed("task_list_not_found", "Task list does not exist");
        }

        return ServiceResult<TaskListViewModel>.Success(_mapper.Map<TaskListViewModel>(taskList));
    }

    public async Task<ServiceResult<List<TaskListViewModel>>> GetTaskLists(
        string? search = null,
        int limit = 100,
        int offset = 0,
        CancellationToken cancellationToken = default
    )
    {
        var projectionQuery = ProjectionQueryExpressionExtensions.Where<TaskListProjectionItem>(t => t.UserAccountId == _userInfo.UserId);
        
        if (search != null)
        {
            projectionQuery.SearchText = search;
        }

        projectionQuery.Limit = limit;
        projectionQuery.Offset = offset;

        projectionQuery.OrderBy = new List<SortInfo>()
        {
            new SortInfo()
            {
                KeyPath = nameof(TaskListProjectionItem.Position),
                Order = "asc"
            }
        };

        var taskLists = await _taskListsProjectionRepository.Query(
            projectionQuery,
            _userInfo.UserId.ToString(),
            cancellationToken
        );

        return ServiceResult<List<TaskListViewModel>>.Success(
            _mapper.Map<List<TaskListViewModel>>(taskLists.Records.Select(r => r.Document))
        );
    }

    public async Task<ServiceResult<List<TaskListWithTasksViewModel>>> GetTaskListsWithTasks(
        string? search = null,
        int limit = 100,
        int offset = 0,
        CancellationToken cancellationToken = default
    )
    {
        using var a = _activitySource.StartActivity("GetTaskListsWithTasks");

        a?.AddEvent(new ActivityEvent("Getting task lists"));
        var taskListsResult = await GetTaskLists(search, limit, offset, cancellationToken);
        a?.AddEvent(new ActivityEvent("Received task lists"));

        if (!taskListsResult.Succeed)
        {
            a?.AddEvent(new ActivityEvent("Failed"));
            return ServiceResult<List<TaskListWithTasksViewModel>>.Failed(taskListsResult.ProblemDetails!);
        }

        a?.AddEvent(new ActivityEvent("Getting tasks"));
        var tasksResult = await GetTasks(string.Join( ",", taskListsResult.Result!.Select(tl => tl.Id.ToString())), cancellationToken);
        a?.AddEvent(new ActivityEvent("Received tasks"));
        
        if (!tasksResult.Succeed)
        {
            return ServiceResult<List<TaskListWithTasksViewModel>>.Failed(tasksResult.ProblemDetails!);
        }

        var taskListsWithTasks = _mapper.Map<List<TaskListWithTasksViewModel>>(taskListsResult.Result);
        
        foreach (var taskList in tasksResult.Result!)
        {
            var list = taskListsWithTasks.First(tl => tl.Id == taskList.Key);
            list.Tasks.AddRange(taskList.Value);
        }

        return ServiceResult<List<TaskListWithTasksViewModel>>.Success(taskListsWithTasks);
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

        var taskList = await _taskListsRepository.LoadAsync(taskListId, _userInfo.UserId.ToString(), cancellationToken);

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

        var taskList = await _taskListsRepository.LoadAsync(request.TaskListId.GetValueOrDefault(), _userInfo.UserId.ToString(), cancellationToken);

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
            _userInfo.UserId.ToString(),
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
            _userInfo.UserId.ToString(),
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
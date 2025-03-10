using Microsoft.AspNetCore.Mvc;
using ToDoList.Models.RequestModels.TaskLists;
using ToDoList.Services.Implementations;
using ToDoList.Services.Interfaces;

namespace ToDoList.Web.Controllers;

public class TasksController : Controller
{
    private readonly ILogger<TasksController> _logger;

    private readonly ITaskListsService _taskListsService;

    public TasksController(ILogger<TasksController> logger, ITaskListsService taskListsService)
    {
        _logger = logger;
        _taskListsService = taskListsService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var tasksLists = await _taskListsService.GetTaskListsWithTasks(null, 9999, 0, cancellationToken);

        return View(tasksLists);
    }
    
    public async Task<IActionResult> TaskLists(CancellationToken cancellationToken)
    {
        var tasksLists = await _taskListsService.GetTaskListsWithTasks(null, 9999, 0, cancellationToken);
        
        return PartialView("TaskLists", tasksLists);
    }
    
    public async Task<IActionResult> CreateTaskList([FromForm] CreateTaskListRequest request, CancellationToken cancellationToken)
    {
        var tasksList = await _taskListsService.CreateTaskList(request, cancellationToken);

        return PartialView("TaskList", tasksList.Result);
    }
    
    public async Task<IActionResult> CreateTask([FromForm] CreateTaskRequest request, CancellationToken cancellationToken)
    {
        var taskResult = await _taskListsService.CreateTask(request, cancellationToken);
        
        return PartialView("Task", taskResult.Result);
    }
    
    public async Task<IActionResult> UpdateTaskPosition([FromForm] UpdateTaskPositionRequest request, CancellationToken cancellationToken)
    {
        var taskResult = await _taskListsService.UpdateTaskPosition(request, cancellationToken);
        
        return PartialView("Task", taskResult.Result);
    }
}
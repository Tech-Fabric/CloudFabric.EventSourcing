using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ToDoList.Api.Extensions;
using ToDoList.Models;
using ToDoList.Models.RequestModels.TaskLists;
using ToDoList.Services.Interfaces;

namespace ToDoList.Api.Controllers;

public class TaskListController : ControllerBase
{
    private readonly ILogger<TaskListController> _logger;
    private readonly ITaskListsService _taskListsService;

    public TaskListController(
        ILogger<TaskListController> logger,
        ITaskListsService taskListsService
    ) {
        _logger = logger;
        _taskListsService = taskListsService;
    }

    [HttpGet("task_list/{taskListId}")]
    public async Task<IActionResult> GetTaskListById(string taskListId, CancellationToken cancellationToken)
    {
        return this.ServiceResult(await _taskListsService.GetTaskListById(taskListId, cancellationToken));
    }

    [HttpPost("task_list")]
    public async Task<IActionResult> CreateTaskList([FromBody] CreateTaskListRequest request, CancellationToken cancellationToken)
    {
        return this.ServiceResult(await _taskListsService.CreateTaskList(request, cancellationToken));
    }

    [HttpPost("task_list/{taskListId}/name")]
    public async Task<IActionResult> UpdateTaskListName(string taskListId, [FromBody] UpdateTaskListNameRequest request, CancellationToken cancellationToken)
    {
        return this.ServiceResult(await _taskListsService.UpdateTaskListName(taskListId, request, cancellationToken));
    }

    [HttpGet("task_list")]
    public async Task<IActionResult> GetTaskLists([FromQuery] string search, [FromQuery] int limit, [FromQuery] int offset, CancellationToken cancellationToken)
    {
        return this.ServiceResult(await _taskListsService.GetTaskLists(search, limit, offset, cancellationToken));
    }

    [HttpGet("task_list/{taskListId}/tasks")]
    public async Task<IActionResult> GetTaskListTasks(string taskListId, [FromQuery] string search, [FromQuery] int limit, [FromQuery] int offset, CancellationToken cancellationToken)
    {
        return this.ServiceResult(await _taskListsService.GetTasks(taskListId, search, limit, offset, cancellationToken));
    }

    [HttpPost("task_list/{taskListId}/tasks")]
    public async Task<IActionResult> CreateTaskListTask([FromQuery] string taskListId, [FromBody] CreateTaskRequest request, CancellationToken cancellationToken)
    {
        request.TaskListId = taskListId;
        return this.ServiceResult(await _taskListsService.CreateTask(request, cancellationToken));
    }
}

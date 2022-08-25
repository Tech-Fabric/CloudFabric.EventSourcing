namespace ToDoList.Models.ViewModels.TaskLists;

public class TaskListViewModel
{
    public string? Name { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int TasksCount { get; set; }
    public int OpenTasksCount { get; set; }
    public int ClosedTasksCount { get; set; }
}

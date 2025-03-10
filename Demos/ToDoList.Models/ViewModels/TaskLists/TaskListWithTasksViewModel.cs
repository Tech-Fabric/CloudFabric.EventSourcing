namespace ToDoList.Models.ViewModels.TaskLists;

public class TaskListWithTasksViewModel : TaskListViewModel
{
    public List<TaskViewModel> Tasks { get; set; } = new List<TaskViewModel>();
}
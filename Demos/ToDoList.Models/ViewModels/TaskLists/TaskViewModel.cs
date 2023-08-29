namespace ToDoList.Models.ViewModels.TaskLists;

public class TaskViewModel
{
    public Guid Id { get; set; }
    public Guid TaskListId { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public bool IsClosed { get; set; }
}
using System.ComponentModel.DataAnnotations;

namespace ToDoList.Models.RequestModels.TaskLists;

public record CreateTaskRequest
{
    [Required]
    public Guid TaskListId { get; set; }

    [Required]
    public string Name { get; set; }

    public string? Description { get; set; }
}

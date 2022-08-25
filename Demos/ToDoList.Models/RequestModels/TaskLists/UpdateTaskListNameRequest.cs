using System.ComponentModel.DataAnnotations;

namespace ToDoList.Models.RequestModels.TaskLists;

public record UpdateTaskListNameRequest
{
    [Required]
    public string Name { get; set; }
}

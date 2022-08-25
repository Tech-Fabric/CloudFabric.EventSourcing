using System.ComponentModel.DataAnnotations;

namespace ToDoList.Models.RequestModels.TaskLists;

public record CreateTaskListRequest
{
    [Required]
    public string Name { get; set; }
}

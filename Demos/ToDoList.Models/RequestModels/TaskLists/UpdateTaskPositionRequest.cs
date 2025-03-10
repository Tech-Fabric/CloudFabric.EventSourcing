using System.ComponentModel.DataAnnotations;
using ToDoList.Models.Validation;

namespace ToDoList.Models.RequestModels.TaskLists;

public record UpdateTaskPositionRequest
{
    [Required]
    [NonEmptyGuid]
    public Guid NewTaskListId { get; set; }

    [Required]
    [NonEmptyGuid]
    public Guid TaskId { get; set; }

    [Required]
    public double NewPosition { get; set; }
}
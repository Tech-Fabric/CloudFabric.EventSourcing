using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ToDoList.Models.RequestModels.UserAccounts;

public record UpdateUserAccountPasswordRequest
{
    [Required]
    public Guid UserAccountId { get; set; }

    [Required]
    [PasswordPropertyText]
    public string OldPassword { get; set; }

    [Required]
    [PasswordPropertyText]
    public string NewPassword { get; set; }
}
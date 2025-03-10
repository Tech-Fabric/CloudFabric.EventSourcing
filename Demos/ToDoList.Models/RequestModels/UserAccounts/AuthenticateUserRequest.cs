using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ToDoList.Models.RequestModels.UserAccounts;

public record AuthenticateUserRequest
{
    [Required(ErrorMessage ="The field '{0}' is required.")]
    [EmailAddress]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    [PasswordPropertyText]
    [Required(ErrorMessage ="The field '{0}' is required.")]
    [Display(Name = "Password")]
    public string? Password { get; set; }
}
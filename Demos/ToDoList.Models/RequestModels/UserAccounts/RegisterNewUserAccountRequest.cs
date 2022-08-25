using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ToDoList.Models.RequestModels.UserAccounts;

public record RegisterNewUserAccountRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; }

    [RegularExpression(@"^[a-zA-Z]+$", ErrorMessage = "Use letters only please")]
    public string FirstName { get; set; }

    [PasswordPropertyText]
    public string Password { get; set; }
}

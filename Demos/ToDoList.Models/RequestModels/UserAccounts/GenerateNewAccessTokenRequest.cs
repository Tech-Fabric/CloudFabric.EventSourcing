using System.ComponentModel.DataAnnotations;

namespace ToDoList.Models.RequestModels.UserAccounts;

public record GenerateNewAccessTokenRequest
{
    [Required]
    [EmailAddress]
    public string? Email { get; set; }

    [Required]
    public string? Password { get; set; }
}
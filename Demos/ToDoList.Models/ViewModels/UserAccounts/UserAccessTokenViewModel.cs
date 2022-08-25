namespace ToDoList.Models.ViewModels.UserAccounts;

public class UserAccessTokenViewModel
{
    public string? AccessToken { get; set; }
    public int ExpiresIn { get; set; }
}

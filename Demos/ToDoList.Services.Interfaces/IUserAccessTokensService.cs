using ToDoList.Models;
using ToDoList.Models.ViewModels.UserAccounts;

namespace ToDoList.Services.Interfaces;

public interface IUserAccessTokensService
{
    public ServiceResult<UserAccessTokenViewModel> GenerateAccessTokenForUser(Guid userAccountId, string userFirstName);
}

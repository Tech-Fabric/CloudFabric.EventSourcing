using System.Security.Claims;
using ToDoList.Models;
using ToDoList.Models.ViewModels.UserAccounts;

namespace ToDoList.Services.Interfaces;

public interface IUserAccessTokensService
{
    public ServiceResult<UserAccessTokenViewModel> GenerateAccessTokenForUser(List<Claim> userClaims);
}
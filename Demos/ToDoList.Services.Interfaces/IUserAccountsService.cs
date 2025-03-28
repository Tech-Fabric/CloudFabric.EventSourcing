using System.Security.Claims;
using ToDoList.Models;
using ToDoList.Models.RequestModels.UserAccounts;
using ToDoList.Models.ViewModels.UserAccounts;

namespace ToDoList.Services.Interfaces;

public interface IUserAccountsService {
    Task<ServiceResult<UserAccountPersonalViewModel>> RegisterNewUserAccount(
        RegisterNewUserAccountRequest request, CancellationToken cancellationToken
    );

    Task<ServiceResult> UpdateUserAccountPassword(
        UpdateUserAccountPasswordRequest request, CancellationToken cancellationToken
    );
    
    Task<ServiceResult<UserAccountPersonalViewModel>> AuthenticateUser(
        AuthenticateUserRequest request, CancellationToken cancellationToken
    );
    
    Task<ServiceResult<UserAccessTokenViewModel>> GenerateAccessTokenForUser(
        AuthenticateUserRequest request, CancellationToken cancellationToken
    );
}
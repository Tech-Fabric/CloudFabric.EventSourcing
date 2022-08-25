using Microsoft.AspNetCore.Mvc;

using ToDoList.Api.Extensions;
using ToDoList.Models.RequestModels.UserAccounts;
using ToDoList.Services.Interfaces;

namespace ToDoList.Api.Controllers;

public class UserController : ControllerBase
{
    private readonly IUserAccountsService _userAccountsService;

    public UserController(IUserAccountsService userAccountsService)
    {
        _userAccountsService = userAccountsService;
    }

    [HttpPost("user_account/register")]
    public async Task<IActionResult> Register([FromBody] RegisterNewUserAccountRequest request, CancellationToken cancellationToken)
    {
        return this.ServiceResult(await _userAccountsService.RegisterNewUserAccount(request, cancellationToken));
    }

    [HttpPost("user_account/token")]
    public async Task<IActionResult> Token([FromBody] GenerateNewAccessTokenRequest request, CancellationToken cancellationToken)
    {
        return this.ServiceResult(await _userAccountsService.GenerateAccessTokenForUser(request, cancellationToken));
    }
}
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using ToDoList.Models;
using ToDoList.Models.RequestModels.UserAccounts;
using ToDoList.Models.ViewModels.UserAccounts;
using ToDoList.Ui.Authentication;

namespace ToDoList.Ui.Services;

public class UserAccountService
{
    private readonly ProtectedSessionStorage _protectedSessionStorage;
    private readonly IServiceCommunicationProvider _serviceCommunicationProvider;
    private readonly TokenAuthenticationStateProvider _authenticationStateProvider;

    public UserAccountService(
        ProtectedSessionStorage protectedSessionStorage,
        IServiceCommunicationProvider serviceCommunicationProvider,
        AuthenticationStateProvider authenticationStateProvider
    )
    {
        _protectedSessionStorage = protectedSessionStorage;
        _serviceCommunicationProvider = serviceCommunicationProvider;
        _authenticationStateProvider = (TokenAuthenticationStateProvider)authenticationStateProvider;
    }

    public async Task<ServiceResult<UserAccountPersonalViewModel>> Register(RegisterNewUserAccountRequest request)
    {
        return await _serviceCommunicationProvider.SendCommand<UserAccountPersonalViewModel>(
            "user_account/register",
            request
        );
    }

    public async Task<ServiceResult<TokenResponse>> Login(GenerateNewAccessTokenRequest request)
    {
        var serviceResult = await _serviceCommunicationProvider.SendCommand<TokenResponse>(
            "user_account/token",
            request
        );

        if (serviceResult.Succeed == true && serviceResult.Result?.AccessToken != null)
        {
            await _protectedSessionStorage.SetAsync("AccessToken", serviceResult.Result?.AccessToken!);

            _authenticationStateProvider.StateChanged();
        }

        return serviceResult;
    }
}

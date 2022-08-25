using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using ToDoList.Models;
using ToDoList.Models.RequestModels.UserAccounts;
using ToDoList.Models.ViewModels.UserAccounts;
using ToDoList.Ui.Authentication;

namespace ToDoList.Ui.Services;

public class UserAccountService
{
    private readonly ProtectedSessionStorage _protectedSessionStore;
    private readonly ILocalStorageService _localStorageService;
    private readonly IServiceCommunicationProvider _serviceCommunicationProvider;
    private readonly TokenAuthenticationStateProvider _authenticationStateProvider;

    public UserAccountService(
        ILocalStorageService localStorageService,
        ProtectedSessionStorage protectedSessionStore,
        IServiceCommunicationProvider serviceCommunicationProvider,
        AuthenticationStateProvider authenticationStateProvider
    )
    {
        _localStorageService = localStorageService;
        _protectedSessionStore = protectedSessionStore;
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

    public async Task<ServiceResult> Login(GenerateNewAccessTokenRequest request)
    {
        var serviceResult = await _serviceCommunicationProvider.SendCommand<TokenResponse>("user_account/token", 
            request
        );

        await _protectedSessionStore.SetAsync("AccessToken", serviceResult.Result?.AccessToken);

        _authenticationStateProvider.StateChanged();

        return serviceResult;
    }
}
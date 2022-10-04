using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using ToDoList.Ui.Services;

namespace ToDoList.Ui.Authentication;

public class TokenResponse
{
    public string AccessToken { get; set; }

    public int ExpiresIn { get; set; }

    public string TokenType { get; set; }

    public string RefreshToken { get; set; }

    public string Scope { get; set; }

    public string Error { get; set; }

    public string ErrorDescription { get; set; }
}

public class TokenRequest
{
    public string Username { get; set; }
    public string Password { get; set; }
}

public class AuthState
{
    private static int lastId = 0;

    public int Id = 0;
        
    public AuthState()
    {
        Id = lastId;
        lastId++;
    }
    public string Token { get; set; }
}

public class TokenAuthenticationStateProvider : Microsoft.AspNetCore.Components.Server.ServerAuthenticationStateProvider
{
    private readonly ProtectedSessionStorage _protectedSessionStore;
    private readonly ILocalStorageService _localStorageService;
    private AuthenticationState? _state;
    private AuthState _authState;

    public TokenAuthenticationStateProvider(
        AuthState authState,
        ILocalStorageService localStorageService,
        ProtectedSessionStorage protectedSessionStore
    )
    {
        _authState = authState;
        _localStorageService = localStorageService;
        _protectedSessionStore = protectedSessionStore;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (_state != null)
        {
            return _state;
        }

        var token = await _protectedSessionStore.GetAsync<string>("AccessToken");
        _authState.Token = token.Value;

        if (string.IsNullOrEmpty(token.Value))
        {
            var identity = new ClaimsIdentity();

            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        else
        {
            var handler = new JwtSecurityTokenHandler();

            if (handler.ReadToken(token.Value) is not JwtSecurityToken jwtToken)
            {
                throw new Exception("Unable to decode access token");
            }

            var identity = new ClaimsIdentity(jwtToken.Claims, "password");
            var user = new ClaimsPrincipal(identity);
            _state = new AuthenticationState(user);
        }

        return _state;
    }

    public void StateChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}

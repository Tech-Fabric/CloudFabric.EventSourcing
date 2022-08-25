using ToDoList.Ui.Services;

namespace ToDoList.Ui.Authentication;

public class AccessTokenMessageHandler : DelegatingHandler
{
    private readonly ILocalStorageService _localStorageService;

    public AccessTokenMessageHandler(ILocalStorageService localStorageService)
    {
        _localStorageService = localStorageService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken
    ) {
        var token = await _localStorageService.GetItem<string>("AccessToken");
        if (token != null)
        {
            request.Headers.Add("Authorization", $"Bearer {token}");
        }
        return await base.SendAsync(request, cancellationToken);
    }
}
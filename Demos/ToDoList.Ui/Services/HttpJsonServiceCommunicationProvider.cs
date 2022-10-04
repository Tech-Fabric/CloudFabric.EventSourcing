using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ToDoList.Models;
using ToDoList.Ui.Authentication;

namespace ToDoList.Ui.Services;

public class HttpJsonServiceCommunicationProvider : IServiceCommunicationProvider {
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AuthState _authState;

    public HttpJsonServiceCommunicationProvider(AuthState authState, IHttpClientFactory httpClientFactory)
    {
        _authState = authState;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<ServiceResult<TViewModel>> Get<TViewModel>(string path) {
        var httpClient = _httpClientFactory.CreateClient("ServerApi");

        var requestMessage = new HttpRequestMessage()
        {
            RequestUri = new Uri(path, UriKind.RelativeOrAbsolute),
            Method = HttpMethod.Get
        };

        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authState.Token);

        var httpResponseMessage = await httpClient.SendAsync(requestMessage);

        if (!httpResponseMessage.IsSuccessStatusCode)
        {
            var problemDetails = await httpResponseMessage.Content.ReadFromJsonAsync<ServiceResultProblemDetails>();
            return ServiceResult<TViewModel>.Failed(problemDetails!);
        }

        var viewModel = await httpResponseMessage.Content.ReadFromJsonAsync<TViewModel>();

        return ServiceResult<TViewModel>.Success(viewModel);
    }

    public async Task<ServiceResult<TViewModel>> SendCommand<TViewModel>(string path, object command) {
        var httpClient = _httpClientFactory.CreateClient("ServerApi");

        var requestContent = new StringContent(
            JsonSerializer.Serialize(command), Encoding.UTF8, "application/json"
        );

        var requestMessage = new HttpRequestMessage()
        {
            RequestUri = new Uri(path, UriKind.RelativeOrAbsolute),
            Method = HttpMethod.Post,
            Content = requestContent
        };
        
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authState.Token);

        var httpResponseMessage = await httpClient.SendAsync(requestMessage);

        if (!httpResponseMessage.IsSuccessStatusCode)
        {
            var stringResult = await httpResponseMessage.Content.ReadAsStringAsync();
            try
            {
                var problemDetails = JsonSerializer.Deserialize<ServiceResultProblemDetails>(stringResult);
                return ServiceResult<TViewModel>.Failed(problemDetails!);
            }
            catch (JsonException ex)
            {
                var problemDetails = new ServiceResultProblemDetails()
                {
                    Detail = "Failed to read response from server",
                    Instance = requestMessage.RequestUri.ToString(),
                    InvalidParams = new List<ServiceResultProblemDetailsInvalidParam>()
                    {
                        new ServiceResultProblemDetailsInvalidParam() { Name = "Response Content", Reason = stringResult }
                    },
                    Title = "Failed to read response from server"
                };
                return ServiceResult<TViewModel>.Failed(problemDetails);
            }
        }

        var viewModel = await httpResponseMessage.Content.ReadFromJsonAsync<TViewModel>();

        return ServiceResult<TViewModel>.Success(viewModel);
    }
}
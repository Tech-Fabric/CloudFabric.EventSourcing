using System.Text;
using System.Text.Json;
using ToDoList.Models;

namespace ToDoList.Ui.Services;

public class HttpJsonServiceCommunicationProvider : IServiceCommunicationProvider {
    private readonly IHttpClientFactory _httpClientFactory;

    public HttpJsonServiceCommunicationProvider(IHttpClientFactory httpClientFactory) {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<ServiceResult<TViewModel>> Get<TViewModel>(string path) {
        var httpClient = _httpClientFactory.CreateClient("ServerApi");

        var requestMessage = new HttpRequestMessage()
        {
            RequestUri = new Uri(path, UriKind.RelativeOrAbsolute),
            Method = HttpMethod.Get
        };

        var httpResponseMessage = await httpClient.SendAsync(requestMessage);

        if (!httpResponseMessage.IsSuccessStatusCode)
        {
            var problemDetails = await httpResponseMessage.Content.ReadFromJsonAsync<ServiceResultProblemDetails>();
            return ServiceResult<TViewModel>.Failed(problemDetails);
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

        var httpResponseMessage = await httpClient.SendAsync(requestMessage);

        if (!httpResponseMessage.IsSuccessStatusCode)
        {
            var problemDetails = await httpResponseMessage.Content.ReadFromJsonAsync<ServiceResultProblemDetails>();
            return ServiceResult<TViewModel>.Failed(problemDetails);
        }

        var viewModel = await httpResponseMessage.Content.ReadFromJsonAsync<TViewModel>();

        return ServiceResult<TViewModel>.Success(viewModel);
    }
}
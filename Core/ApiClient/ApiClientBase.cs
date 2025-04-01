using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Core.ApiClient;

public abstract class ApiClientBase<TResponse>(HttpClient httpClient,
                                               ILogger<ApiClientBase<TResponse>> logger) where TResponse : ResponseBase
{
    protected readonly HttpClient _httpClient = httpClient;
    protected readonly ILogger<ApiClientBase<TResponse>> _logger = logger;

    public static readonly JsonSerializerOptions ApiJsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    protected async Task<IRequestResult> ApiCall(HttpRequestMessage request)
    {
        var result = await ApiCall<TResponse>(request);

        return result.IsSuccessful switch
        {
            true => RequestResult.Success(),
            false => RequestResult.Failure(result.Error!)
        };
    }

    protected virtual Task<IRequestResult<T>> ApiCall<T>(HttpRequestMessage request) where T : TResponse =>
        ApiCall<T>(_httpClient, request, _logger);

    public virtual Task<IRequestResult<T>> ApiCall<T>(HttpClient httpClient, HttpRequestMessage request) where T : TResponse =>
        ApiCall<T>(httpClient, request, null);

    public virtual async Task<IRequestResult<T>> ApiCall<T>(HttpClient httpClient, HttpRequestMessage request, ILogger? logger) where T : TResponse
    {
        var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        T result = (await response.Content.ReadFromJsonAsync<T>(ApiJsonSerializerOptions))!;

        logger?.LogDebug("API response {HttpMethod} {RequestUri}:\n{ResponseMessage}",
            request.Method,
            request.RequestUri,
            await response.Content.ReadAsStringAsync());

        return RequestResult<T>.Success(result);
    }
}

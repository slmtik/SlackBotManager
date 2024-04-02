using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using System.Web;
using SlackBotManager.API.Models.SlackClient;
using SlackBotManager.API.Interfaces;

namespace SlackBotManager.API.Services;

public class SlackClient
{
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<SlackClient> _logger;
    private readonly IWebHostEnvironment _env;

    public SlackClient(HttpClient httpClient, IConfiguration configuration, ILogger<SlackClient> logger, IWebHostEnvironment env)
    {
        _httpClient = httpClient;
        _logger = logger;
        _env = env;
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", configuration["Slack:SLACK_BOT_TOKEN"]);
    }

    private Task<SlackResponse> ApiCall(string requestUri, HttpMethod httpMethod, HttpContent? content = null) =>
        ApiCall<SlackResponse>(requestUri, httpMethod, content);

    private async Task<T> ApiCall<T>(string requestUri, HttpMethod httpMethod, HttpContent? content = null) where T : SlackResponse
    {
        HttpRequestMessage request = new(httpMethod, requestUri) { Content = content };
        var responseMessage = await _httpClient.SendAsync(request);
        responseMessage.EnsureSuccessStatusCode();
        var result = await responseMessage.Content.ReadFromJsonAsync<T>(_jsonSerializerOptions);
        
        if (_env.IsDevelopment())
            _logger.LogInformation("Slack Api response {HttpMethod} {RequestUri}:\n{ResponseMessage}", 
                httpMethod,
                requestUri,
                await responseMessage.Content.ReadAsStringAsync());

        if (!result!.Ok)
        {
            if (result.ResponseMetadata != null)
                _logger.LogWarning("Slack API request {HttpMethod} {RequestUri} was not OK. There are {ErrorNumber} error(s). \n{ResponseMetadata}",
                               httpMethod,
                               requestUri,
                               result.ResponseMetadata.Messages.Length,
                               string.Join("\n", result.ResponseMetadata.Messages));
            else
                _logger.LogError("Slack API error {SlackError}", result.Error);

        }

        return result!;
    }

    #region Chat
    public Task<ChatPostMessageResponse> ChatPostMessage(ChatPostMessageRequest message)
    {
        string body = JsonSerializer.Serialize(message, _jsonSerializerOptions);
        StringContent content = new(body, Encoding.UTF8, "application/json");
        return ApiCall<ChatPostMessageResponse>("chat.postMessage", HttpMethod.Post, content);
    }

    public Task<SlackResponse> ChatUpdateMessage(ChaUpdateMessageRequest message)
    {
        string body = JsonSerializer.Serialize(message, _jsonSerializerOptions);
        StringContent content = new(body, Encoding.UTF8, "application/json");
        return ApiCall<SlackResponse>("chat.update", HttpMethod.Post, content);
    }

    public Task<SlackResponse> ChatDeleteMessage(string channel, string timestampId)
    {
        var body = JsonSerializer.Serialize(new { channel, ts = timestampId }, _jsonSerializerOptions);
        StringContent content = new(body, Encoding.UTF8, "application/json");
        return ApiCall<SlackResponse>("chat.delete", HttpMethod.Post, content);
    }
    #endregion

    #region Users
    public Task<UserInfoResponse> UserInfo(string userId)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["user"] = userId;

        return ApiCall<UserInfoResponse>($"users.info?{query}", HttpMethod.Get);
    }
    #endregion

    #region Views
    public Task<SlackResponse> ViewOpen(string triggerId, string viewPayload)
    {
        var body = JsonSerializer.Serialize(new { triggerId, view = viewPayload }, _jsonSerializerOptions);
        StringContent content = new(body, Encoding.UTF8, "application/json");
        return ApiCall("views.open", HttpMethod.Post, content);
    }

    public Task<SlackResponse> ViewPush(string triggerId, string viewPayload)
    {
        var body = JsonSerializer.Serialize(new { triggerId, view = viewPayload }, _jsonSerializerOptions);
        StringContent content = new(body, Encoding.UTF8, "application/json");
        return ApiCall("views.push", HttpMethod.Post, content);
    }

    public Task<SlackResponse> ViewUpdate(string viewId, string viewPayload)
    {
        var body = JsonSerializer.Serialize(new { viewId, view = viewPayload }, _jsonSerializerOptions);
        StringContent content = new(body, Encoding.UTF8, "application/json");
        return ApiCall("views.update", HttpMethod.Post, content);
    }
    #endregion
}

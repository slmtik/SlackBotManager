using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using System.Web;
using SlackBotManager.API.Models.SlackClient;

namespace SlackBotManager.API.Services;

public class SlackClient(HttpClient httpClient,
                         IConfiguration configuration,
                         ILogger<SlackClient> logger,
                         IWebHostEnvironment env,
                         IHttpContextAccessor httpContextAccessor)
{
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger<SlackClient> _logger = logger;
    private readonly IWebHostEnvironment _env = env;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private readonly string _clientId = configuration["Slack:ClientId"] ?? throw new ArgumentException("Slack ClientId is not provided");
    private readonly string _clientSecret = configuration["Slack:ClientSecret"] ?? throw new ArgumentException("Slack ClientSecret is not provided");

    private Task<SlackResponse> ApiCall(HttpRequestMessage request) =>
        ApiCall<SlackResponse>(request);

    private async Task<T> ApiCall<T>(HttpRequestMessage request) where T : SlackResponse
    {
        request.Headers.Authorization ??= new AuthenticationHeaderValue("Bearer", _httpContextAccessor.HttpContext!.Items["bot-token"]!.ToString());

        var responseMessage = await _httpClient.SendAsync(request);
        responseMessage.EnsureSuccessStatusCode();
        var result = await responseMessage.Content.ReadFromJsonAsync<T>(_jsonSerializerOptions);
        
        if (_env.IsDevelopment())
            _logger.LogInformation("Slack Api response {HttpMethod} {RequestUri}:\n{ResponseMessage}", 
                request.Method,
                request.RequestUri,
                await responseMessage.Content.ReadAsStringAsync());

        if (!result!.Ok)
        {
            if (result.ResponseMetadata != null)
                _logger.LogError("Slack API request {HttpMethod} {RequestUri} was not OK. There are {ErrorNumber} error(s). \n{ResponseMetadata}",
                               request.Method,
                               request.RequestUri,
                               result.ResponseMetadata.Messages?.Length,
                               string.Join("\n", result.ResponseMetadata.Messages ?? []));
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
        return ApiCall<ChatPostMessageResponse>(new(HttpMethod.Post, "chat.postMessage") { Content = content });
    }

    public Task<SlackResponse> ChatUpdateMessage(ChaUpdateMessageRequest message)
    {
        string body = JsonSerializer.Serialize(message, _jsonSerializerOptions);
        StringContent content = new(body, Encoding.UTF8, "application/json");
        return ApiCall<SlackResponse>(new(HttpMethod.Post, "chat.update") { Content = content });
    }

    public Task<SlackResponse> ChatDeleteMessage(string channel, string timestampId)
    {
        var body = JsonSerializer.Serialize(new { channel, ts = timestampId }, _jsonSerializerOptions);
        StringContent content = new(body, Encoding.UTF8, "application/json");
        return ApiCall<SlackResponse>(new(HttpMethod.Post, "chat.delete") { Content = content });
    }
    #endregion

    #region Users
    public Task<UserInfoResponse> UserInfo(string userId)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["user"] = userId;

        return ApiCall<UserInfoResponse>(new(HttpMethod.Get, $"users.info?{query}"));
    }
    #endregion

    #region Views
    public Task<SlackResponse> ViewOpen(string triggerId, string viewPayload)
    {
        var body = JsonSerializer.Serialize(new { triggerId, view = viewPayload }, _jsonSerializerOptions);
        StringContent content = new(body, Encoding.UTF8, "application/json");
        return ApiCall(new(HttpMethod.Post, "views.open") { Content = content });
    }

    public Task<SlackResponse> ViewPush(string triggerId, string viewPayload)
    {
        var body = JsonSerializer.Serialize(new { triggerId, view = viewPayload }, _jsonSerializerOptions);
        StringContent content = new(body, Encoding.UTF8, "application/json");
        return ApiCall(new(HttpMethod.Post, "views.push") { Content = content });
    }

    public Task<SlackResponse> ViewUpdate(string viewId, string viewPayload)
    {
        var body = JsonSerializer.Serialize(new { viewId, view = viewPayload }, _jsonSerializerOptions);
        StringContent content = new(body, Encoding.UTF8, "application/json");
        return ApiCall(new(HttpMethod.Post, "views.update") { Content = content });
    }
    #endregion

    #region OAuth
    public Task<OAuthResponse> OAuthV2Success(OAuthV2SuccessRequest message)
    {
        FormUrlEncodedContent content = new(message.ToDictionary());
        HttpRequestMessage request = new(HttpMethod.Post, "oauth.v2.access") { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}")));
        return ApiCall<OAuthResponse>(request);
    }

    public Task<AuthTestResponse> AuthTest(string accessToken)
    {
        HttpRequestMessage request = new(HttpMethod.Post, "auth.test");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return ApiCall<AuthTestResponse>(request);
    }
    #endregion
}

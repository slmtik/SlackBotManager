using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using System.Web;
using SlackBotManager.API.Models.SlackClient;
using SlackBotManager.API.Models.Surfaces;
using SlackBotManager.API.Models.Views;

namespace SlackBotManager.API.Services;

public class SlackClient(HttpClient httpClient,
                         IConfiguration configuration,
                         ILogger<SlackClient> logger,
                         IHttpContextAccessor httpContextAccessor)
{

    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger<SlackClient> _logger = logger;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private readonly string _clientId = configuration["Slack:ClientId"] ?? throw new ArgumentException("Slack ClientId is not provided");
    private readonly string _clientSecret = configuration["Slack:ClientSecret"] ?? throw new ArgumentException("Slack ClientSecret is not provided");

    public static readonly JsonSerializerOptions SlackJsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private Task<BaseResponse> ApiCall(HttpRequestMessage request) =>
        ApiCall<BaseResponse>(request);

    private async Task<T> ApiCall<T>(HttpRequestMessage request) where T : BaseResponse
    {
        request.Headers.Authorization ??= new AuthenticationHeaderValue("Bearer", _httpContextAccessor.HttpContext!.Items["bot-token"]!.ToString());

        var responseMessage = await _httpClient.SendAsync(request);
        responseMessage.EnsureSuccessStatusCode();
        var result = await responseMessage.Content.ReadFromJsonAsync<T>(SlackJsonSerializerOptions);
        
        _logger.LogDebug("Slack Api response {HttpMethod} {RequestUri}:\n{ResponseMessage}", 
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

        return result;
    }

    #region Chat
    public Task<ChatPostMessageResponse> ChatPostMessage(ChatPostMessageRequest message)
    {
        string body = JsonSerializer.Serialize(message, SlackJsonSerializerOptions);
        StringContent content = new(body, Encoding.UTF8, "application/json");
        return ApiCall<ChatPostMessageResponse>(new(HttpMethod.Post, "chat.postMessage") { Content = content });
    }

    public Task<BaseResponse> ChatUpdateMessage(ChaUpdateMessageRequest message)
    {
        string body = JsonSerializer.Serialize(message, SlackJsonSerializerOptions);
        StringContent content = new(body, Encoding.UTF8, "application/json");
        return ApiCall<BaseResponse>(new(HttpMethod.Post, "chat.update") { Content = content });
    }

    public Task<BaseResponse> ChatDeleteMessage(string channel, string timestampId)
    {
        var body = JsonSerializer.Serialize(new { channel, ts = timestampId }, SlackJsonSerializerOptions);
        StringContent content = new(body, Encoding.UTF8, "application/json");
        return ApiCall<BaseResponse>(new(HttpMethod.Post, "chat.delete") { Content = content });
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
    public Task<BaseResponse> ViewOpen(string triggerId, ModalView modalView)
    {
        var body = JsonSerializer.Serialize(new { triggerId, view = modalView }, SlackJsonSerializerOptions);
        StringContent content = new(body, Encoding.UTF8, "application/json");
        return ApiCall(new(HttpMethod.Post, "views.open") { Content = content });
    }

    public Task<BaseResponse> ViewPush(string triggerId, ModalView modalView)
    {
        var body = JsonSerializer.Serialize(new { triggerId, view = modalView }, SlackJsonSerializerOptions);
        StringContent content = new(body, Encoding.UTF8, "application/json");
        return ApiCall(new(HttpMethod.Post, "views.push") { Content = content });
    }

    public Task<BaseResponse> ViewUpdate(string viewId, ModalView modalView)
    {
        var body = JsonSerializer.Serialize(new { viewId, view = modalView }, SlackJsonSerializerOptions);
        StringContent content = new(body, Encoding.UTF8, "application/json");
        return ApiCall(new(HttpMethod.Post, "views.update") { Content = content });
    }

    public Task<BaseResponse> ViewPublish(string user_id, HomeView homeView)
    {
        var body = JsonSerializer.Serialize(new { user_id, view = homeView }, SlackJsonSerializerOptions);
        StringContent content = new(body, Encoding.UTF8, "application/json");
        return ApiCall(new(HttpMethod.Post, "views.publish") { Content = content });
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

    # region Conversations
    public Task<ConversationInfoResponse> ConversationsInfo(string channelId)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["channel"] = channelId;

        return ApiCall<ConversationInfoResponse>(new(HttpMethod.Get, $"conversations.info?{query}"));
    }
    #endregion
}

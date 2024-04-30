using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using System.Web;
using SlackBotManager.API.Models.SlackClient;
using SlackBotManager.API.Models.Surfaces;
using SlackBotManager.API.Models.Views;
using SlackBotManager.API.Interfaces;
using SlackBotManager.API.Models.Core;

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

    public const string BotTokenHttpContextKey = "bot_token";

    public static readonly JsonSerializerOptions SlackJsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private async Task<IRequestResult> ApiCall(HttpRequestMessage request)
    {
        var result = await ApiCall<BaseResponse>(request);

        return result.IsSuccesful switch
        {
            true => RequestResult.Success(),
            false => RequestResult.Failure(result.Error!)
        };
    }

    private async Task<IRequestResult<T>> ApiCall<T>(HttpRequestMessage request) where T : BaseResponse 
    {
        request.Headers.Authorization ??= new AuthenticationHeaderValue("Bearer", _httpContextAccessor.HttpContext.Items[BotTokenHttpContextKey].ToString());

        var responseMessage = await _httpClient.SendAsync(request);
        responseMessage.EnsureSuccessStatusCode();
        T result = (await responseMessage.Content.ReadFromJsonAsync<T>(SlackJsonSerializerOptions))!;
        
        _logger.LogDebug("Slack Api response {HttpMethod} {RequestUri}:\n{ResponseMessage}", 
            request.Method,
            request.RequestUri,
            await responseMessage.Content.ReadAsStringAsync());

        if (!result.Ok)
        {
            _logger.LogError("Slack API error {HttpMethod} {RequestUri} {SlackError}\n{ResponseMetadata}",
                             request.Method,
                             request.RequestUri,
                             result.Error,
                             string.Join("\n", result.ResponseMetadata?.Messages ?? []));
            return RequestResult<T>.Failure(result.Error ?? string.Empty);
        }

        return RequestResult<T>.Success(result);
    }

    #region Chat
    public Task<IRequestResult<ChatPostMessageResponse>> ChatPostMessage(ChatPostMessageRequest message)
    {
        string body = JsonSerializer.Serialize(message, SlackJsonSerializerOptions);
        StringContent content = new(body, Encoding.UTF8, "application/json");
        return ApiCall<ChatPostMessageResponse>(new(HttpMethod.Post, "chat.postMessage") { Content = content });
    }

    public Task<IRequestResult> ChatUpdateMessage(ChaUpdateMessageRequest message)
    {
        string body = JsonSerializer.Serialize(message, SlackJsonSerializerOptions);
        StringContent content = new(body, Encoding.UTF8, "application/json");
        return ApiCall(new(HttpMethod.Post, "chat.update") { Content = content });
    }

    public Task<IRequestResult> ChatDeleteMessage(string channel, string timestamp)
    {
        var body = JsonSerializer.Serialize(new { channel, ts = timestamp }, SlackJsonSerializerOptions);
        StringContent content = new(body, Encoding.UTF8, "application/json");
        return ApiCall(new(HttpMethod.Post, "chat.delete") { Content = content });
    }
    #endregion

    #region Users
    public Task<IRequestResult<UserInfoResponse>> UserInfo(string userId)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["user"] = userId;

        return ApiCall<UserInfoResponse>(new(HttpMethod.Get, $"users.info?{query}"));
    }
    #endregion

    #region Views
    public Task<IRequestResult> ViewOpen(string triggerId, ModalView modalView)
    {
        var body = JsonSerializer.Serialize(new { triggerId, view = modalView }, SlackJsonSerializerOptions);
        StringContent content = new(body, Encoding.UTF8, "application/json");
        return ApiCall(new(HttpMethod.Post, "views.open") { Content = content });
    }

    public Task<IRequestResult> ViewPush(string triggerId, ModalView modalView)
    {
        var body = JsonSerializer.Serialize(new { triggerId, view = modalView }, SlackJsonSerializerOptions);
        StringContent content = new(body, Encoding.UTF8, "application/json");
        return ApiCall(new(HttpMethod.Post, "views.push") { Content = content });
    }

    public Task<IRequestResult> ViewUpdate(string viewId, ModalView modalView)
    {
        var body = JsonSerializer.Serialize(new { viewId, view = modalView }, SlackJsonSerializerOptions);
        StringContent content = new(body, Encoding.UTF8, "application/json");
        return ApiCall(new(HttpMethod.Post, "views.update") { Content = content });
    }

    public Task<IRequestResult> ViewPublish(string user_id, HomeView homeView)
    {
        var body = JsonSerializer.Serialize(new { user_id, view = homeView }, SlackJsonSerializerOptions);
        StringContent content = new(body, Encoding.UTF8, "application/json");
        return ApiCall(new(HttpMethod.Post, "views.publish") { Content = content });
    }
    #endregion

    #region OAuth
    public Task<IRequestResult<OAuthResponse>> OAuthV2Success(OAuthV2SuccessRequest message)
    {
        FormUrlEncodedContent content = new(message.ToDictionary());
        HttpRequestMessage request = new(HttpMethod.Post, "oauth.v2.access") { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}")));
        return ApiCall<OAuthResponse>(request);
    }

    public Task<IRequestResult<AuthTestResponse>> AuthTest(string accessToken)
    {
        HttpRequestMessage request = new(HttpMethod.Post, "auth.test");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return ApiCall<AuthTestResponse>(request);
    }
    #endregion

    # region Conversations
    public Task<IRequestResult<ConversationInfoResponse>> ConversationsInfo(string channelId)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["channel"] = channelId;

        return ApiCall<ConversationInfoResponse>(new(HttpMethod.Get, $"conversations.info?{query}"));
    }
    #endregion
}

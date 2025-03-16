using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text;
using Jira.DTO;
using Microsoft.Extensions.Logging;
using Core.ApiClient;

namespace Jira;

public class JiraClient(HttpClient httpClient,
                        ILogger<JiraClient> logger,
                        IConfiguration configuration) : BaseApiClient<BaseResponse>(httpClient, logger)
{
    private readonly string _clientId = configuration["IssueTracker:ClientId"] ?? throw new ArgumentException("IssueTracker ClientId is not provided");
    private readonly string _clientSecret = configuration["IssueTracker:ClientSecret"] ?? throw new ArgumentException("IssueTracker ClientSecret is not provided");

    protected override Task<IRequestResult<T>> ApiCall<T>(HttpRequestMessage request)
    {
        return base.ApiCall<T>(request);
    }

    public async Task<IRequestResult<TokenResponse>> GetOAuthToken(string code, string callbackUrl)
    {
        string body = JsonSerializer.Serialize(new {
            grantType = "authorization_code",
            clientId = _clientId,
            clientSecret = _clientSecret,
            code,
            RedirectUri = callbackUrl
        }, ApiJsonSerializerOptions);

        StringContent content = new(body, Encoding.UTF8, "application/json");
        HttpRequestMessage request = new(HttpMethod.Post, "https://auth.atlassian.com/oauth/token") { Content = content };

        return await ApiCall<TokenResponse>(request);
    }
}

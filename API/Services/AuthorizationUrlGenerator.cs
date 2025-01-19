namespace API.Services;

public class AuthorizationUrlGenerator(IConfiguration configuration)
{
    private const string _authorizationUrl = "https://slack.com/oauth/v2/authorize";

    private readonly string _clienId = configuration["Slack:ClientId"] ?? throw new ArgumentException("Slack ClientId is not provided");
    private readonly string[]? _scopes = configuration.GetSection("Slack:Scopes").Get<string[]?>();
    private readonly string[]? _userScopes = configuration.GetSection("Slack:UserScopes").Get<string[]?>();

    public string Generate(string state)
    {
        var scopes = string.Join(",", _scopes ?? []);
        var userScopes = string.Join(",", _userScopes ?? []);

        var queryParams = string.Join("&", $"state={state}", $"client_id={_clienId}", $"scope={scopes}", $"user_scope={userScopes}");

        return $"{_authorizationUrl}?{queryParams}";
    }
}

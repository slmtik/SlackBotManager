using Slack.Models.SlackClient;

namespace Slack.Models.SlackClient;

public class OAuthResponse : BaseResponse
{
    public Enterprise? Enterprise { get; set; }
    public bool IsEnterpriseInstall { get; set; }
    public Team? Team { get; set; }
    public AuthedUser? AuthedUser { get; set; }
    public string? AccessToken { get; set; }
    public string? AppId { get; set; }
    public string? Scope { get; set; }
    public string? TokenType { get; set; }
    public string? BotUserId { get; set; }
    public string? RefreshToken { get; set; }
    public int ExpiresIn { get; set; }
}

public class Enterprise
{
    public string? Id { get; set; }
    public string? Name { get; set; }
}
public class Team
{
    public string? Id { get; set; }
    public string? Name { get; set; }
}

public class AuthedUser
{
    public string? Id { get; set; }
    public string? AccessToken { get; internal set; }
    public string? Scope { get; internal set; }
    public string? RefreshToken { get; internal set; }
    public int? ExpiresIn { get; internal set; }
}
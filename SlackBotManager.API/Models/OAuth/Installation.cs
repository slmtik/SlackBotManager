
namespace SlackBotManager.API.Models.OAuth;

public class Installation
{
    private int? _userTokenExpiresIn;
    private int? _botTokenExpiresIn;

    public string? AppId { get; set; }
    public string? EnterpriseId { get; set; }
    public string? EnterpriseName { get; set; }
    public string? EnterpriseUrl { get; set; }
    public string? TeamId { get; set; }
    public string? TeamName { get; set; }
    public string? BotToken { get; set; }
    public string? BotId { get; set; }
    public string? BotUserId { get; set; }
    public string? BotScopes { get; set; }
    public string? BotRefreshToken { get; set; }
    public int? BotTokenExpiresIn 
    { 
        get => _botTokenExpiresIn;
        set
        {
            _botTokenExpiresIn = value;
            BotTokenExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + _botTokenExpiresIn;
        }
    }
    public string? UserToken { get; set; }
    public string? UserId { get; set; }
    public string? UserScopes { get; set; }
    public string? UserRefreshToken { get; set; }
    public int? UserTokenExpiresIn 
    { 
        get => _userTokenExpiresIn;
        set
        {
            _userTokenExpiresIn = value;
            UserTokenExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + _userTokenExpiresIn;
        }
    }
    public bool IsEnterpriseInstall { get; set; }
    public string? TokenType { get; set; }
    public long? BotTokenExpiresAt { get; set; }
    public string? CustomValues { get; set; }
    public string InstalledAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
    public long? UserTokenExpiresAt { get; set; }

    public Installation()
    {
        
    }
    public Installation(Installation installation)
    {
        AppId = installation.AppId;
        EnterpriseId = installation.EnterpriseId;
        EnterpriseName = installation.EnterpriseName;
        EnterpriseUrl = installation.EnterpriseUrl;
        TeamId = installation.TeamId;
        TeamName = installation.TeamName;
        BotToken = installation.BotToken;
        BotId = installation.BotId;
        BotUserId = installation.BotUserId;
        BotScopes = installation.BotScopes;
        BotRefreshToken = installation.BotRefreshToken;
        BotTokenExpiresIn = installation.BotTokenExpiresIn;
        UserToken = installation.UserToken;
        UserId = installation.UserId;
        UserScopes = installation.UserScopes;
        UserRefreshToken = installation.UserRefreshToken;
        UserTokenExpiresIn = installation.UserTokenExpiresIn;
        IsEnterpriseInstall = installation.IsEnterpriseInstall;
        TokenType = installation.TokenType;
        BotTokenExpiresAt = installation.BotTokenExpiresAt;
        CustomValues = installation.CustomValues;
        InstalledAt = installation.InstalledAt;
        UserTokenExpiresAt = installation.UserTokenExpiresAt;
    }

    public Bot ToBot()
    {
        return new()
        {
            AppId = AppId,
            BotId = BotId,
            BotRefreshToken = BotRefreshToken,
            BotScopes = BotScopes,
            BotToken = BotToken,
            BotTokenExpiresAt = BotTokenExpiresAt,
            BotUserId = BotUserId,
            CustomValues = CustomValues,
            EnterpriseId = EnterpriseId,
            EnterpriseName = EnterpriseName,
            InstalledAt = InstalledAt,
            IsEnterpriseInstall = IsEnterpriseInstall,
            TeamId = TeamId,
            TeamName = TeamName
        };
    }
}
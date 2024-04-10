namespace SlackBotManager.API.Models.Stores;

public class Bot
{
    public string? AppId { get; set; }
    public string? EnterpriseId { get; set; }
    public string? EnterpriseName { get; set; }
    public string? TeamId { get; set; }
    public string? TeamName { get; set; }
    public string? BotToken { get; set; }
    public string? BotId { get; set; }
    public string? BotUserId { get; set; }
    public string? BotScopes { get; set; }
    public string? BotRefreshToken { get; set; }
    public long? BotTokenExpiresAt { get; set; }
    public bool IsEnterpriseInstall { get; set; }
    public string? InstalledAt { get; set; }
    public string? CustomValues { get; set; }

    public Bot()
    {
        
    }

    public Bot(Bot other)
    {
        AppId = other.AppId;
        EnterpriseId = other.EnterpriseId;
        EnterpriseName = other.EnterpriseName;
        TeamId = other.TeamId;
        TeamName = other.TeamName;
        BotToken = other.BotToken;
        BotId = other.BotId;
        BotUserId = other.BotUserId;
        BotScopes = other.BotScopes;
        BotRefreshToken = other.BotRefreshToken;
        BotTokenExpiresAt = other.BotTokenExpiresAt;
        IsEnterpriseInstall = other.IsEnterpriseInstall;
        InstalledAt = other.InstalledAt;
        CustomValues = other.CustomValues;
    }
}
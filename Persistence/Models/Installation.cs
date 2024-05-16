namespace SlackBotManager.Persistence.Models;

public record Installation : StoreItemBase
{
    private int? _userTokenExpiresIn;
    private int? _botTokenExpiresIn;

    public string? AppId { get; set; }
    public string? EnterpriseName { get; set; }
    public string? EnterpriseUrl { get; set; }
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
    public string? TokenType { get; set; }
    public long? BotTokenExpiresAt { get; set; }
    public string? CustomValues { get; set; }
    public string InstalledAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
    public long? UserTokenExpiresAt { get; set; }
}
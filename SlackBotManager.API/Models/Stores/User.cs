namespace SlackBotManager.API.Models.Stores;

public class User
{
    public string? Token { get; set; }
    public string? Id { get; set; }
    public string? UserId { get; set; }
    public string? Scopes { get; set; }
    public string? RefreshToken { get; set; }
    public int? TokenExpiresIn { get; set; }
}

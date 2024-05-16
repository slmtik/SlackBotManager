namespace SlackBotManager.Slack;

public class OAuthV2SuccessRequest
{
    public string? Code { get; set; }
    public string? GrantType { get; set; }
    public string? RefreshToken { get; set; }

    public Dictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string?>
        {
            { "code", Code },
            { "grant_type", GrantType },
            { "refresh_token", RefreshToken }
        }
        .Where(kvp => kvp.Value != null)
        .Select(kvp => new KeyValuePair<string, string>(kvp.Key, kvp.Value!))
        .ToDictionary();
    }
}

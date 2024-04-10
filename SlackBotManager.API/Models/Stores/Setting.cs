namespace SlackBotManager.API.Models.Stores;

public class Setting
{
    public string? EnterpriseId { get; set; }
    public bool IsEnterpriseInstall { get; set; }
    public string? TeamId { get; set; }
    public string? CreatePullRequestChannelId { get; set; }
    public string[]? ApplicationAdminUsers { get; set; }
}

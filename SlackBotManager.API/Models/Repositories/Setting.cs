namespace SlackBotManager.API.Models.Repositories;

public class Setting
{
    public string? EnterpriseId { get; set; }
    public bool IsEnterpriseInstall { get; set; }
    public string? TeamId { get; set; }
    public string? CreatePullRequestChannelId { get; set; }
    public IEnumerable<string> Branches { get; set; } = ["develop", "release"];
    public IEnumerable<string> Tags { get; set; } = ["#usefull", "#easy"];
    public string[]? ApplicationAdminUsers { get; set; }
    public PullRequestReview? CurrentPullRequestReview { get; set; }
}

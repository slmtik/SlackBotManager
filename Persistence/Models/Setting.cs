namespace Persistence.Models;

public record Setting : StoreItemBase
{
    public string? CreatePullRequestChannelId { get; set; }
    public IEnumerable<string> Branches { get; set; } = ["develop", "release"];
    public IEnumerable<string> Tags { get; set; } = ["#usefull", "#easy"];
    public string[]? ApplicationAdminUsers { get; set; }
    public ReminderSetting? ReminderSetting { get; set; }
}

using Microsoft.AspNetCore.Mvc;

namespace SlackBotManager.API.Models.Commands;

public class Command
{
    [BindProperty(Name = "command")]
    public required string CommandText { get; set; }

    [BindProperty(Name = "user_id")]
    public string UserId { get; set; }
    [BindProperty(Name = "trigger_id")]
    public string TriggerId { get; set; }
    [BindProperty(Name = "channel_id")]
    public string ChannelId { get; set; }
    [BindProperty(Name = "team_id")]
    public string? TeamId { get; set; }
    [BindProperty(Name = "enterprise_id")]
    public string? EnterpriseId { get; set; }
    [BindProperty(Name = "is_enterprise_install")]
    public bool IsEnterpriseInstall { get; set; }
    [BindProperty(Name = "response_url")]
    public string? ResponseUrl { get; set; }
}

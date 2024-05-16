using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

namespace SlackBotManager.Slack.Events;

public class Authorization
{
    [BindProperty(Name = "enterprise_id")]
    public string? EnterpriseId { get; set; }
    [BindProperty(Name = "is_enterprise_install")]
    public bool IsEnterpriseInstall { get; set; }
    [JsonPropertyName("team_id")]
    public string? TeamId { get; set; }
    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }
}
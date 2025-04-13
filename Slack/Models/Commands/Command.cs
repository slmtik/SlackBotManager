using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

namespace Slack.Models.Commands;

public class Command
{
    [FromForm(Name = "command")]
    [JsonPropertyName("command")]
    public string CommandText { get; set; }
    [FromForm(Name = "user_id")]
    public string UserId { get; set; }
    [FromForm(Name = "trigger_id")]
    public string TriggerId { get; set; }
    [FromForm(Name = "channel_id")]
    public string ChannelId { get; set; }
    [FromForm(Name = "team_id")]
    public string? TeamId { get; set; }
    [FromForm(Name = "enterprise_id")]
    public string? EnterpriseId { get; set; }
    [FromForm(Name = "is_enterprise_install")]
    [JsonConverter(typeof(SlackStringBooleanConverter))]
    public bool IsEnterpriseInstall { get; set; }
    public string? ResponseUrl { get; set; }
}

using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

namespace SlackBotManager.API.Models.Events;

public class Authorization
{
    [BindProperty(Name = "enterprise_id")]
    public string? EnterpriseId { get; set; }
    [BindProperty(Name = "is_enterprise_install")]
    public bool IsEnterpriseInstall { get; set; }
    [JsonPropertyName("team_id")]
    public string? TeamId { get; set; }
}
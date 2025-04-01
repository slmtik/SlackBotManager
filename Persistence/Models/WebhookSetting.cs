namespace Persistence.Models;

public class WebhookSetting
{
    public string? WebhookUrl { get; set; }
    public string WebhookHeader { get; set; } = "X-Automation-Webhook-Token";
    public string? WebhookSecret { get; set; }
    public string? MessageTemplate { get; set; } = "{\r\n    \"issues\": [%Issues%],\r\n    \"data\": {\r\n        \"fixedIn\": %Versions%\r\n    }\r\n}";
    public Dictionary<string, VersionStrategy> VersionStrategies { get; set; } = [];
} 
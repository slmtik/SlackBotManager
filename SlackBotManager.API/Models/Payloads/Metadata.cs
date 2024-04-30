using System.Text.Json.Nodes;

namespace SlackBotManager.API.Models.Payloads;

public class Metadata
{
    public string? EventType { get; set; }
    public JsonObject? EventPayload { get; set; }
}

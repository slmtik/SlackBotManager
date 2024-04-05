using System.Text.Json.Nodes;

namespace SlackBotManager.API.Models.SlackClient
{
    public class Metadata
    {
        public string? EventType { get; set; }
        public JsonObject? EventPayload { get; set; }
    }
}

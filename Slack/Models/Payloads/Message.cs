using SlackBotManager.Slack.Blocks;
using System.Text.Json.Serialization;

namespace SlackBotManager.Slack.Payloads;

public class Message
{
    [JsonPropertyName("ts")]
    public string Timestamp { get; set; }
    public IEnumerable<IBlock> Blocks { get; set; }
    public Metadata? Metadata { get; set; }
}
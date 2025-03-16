using System.Text.Json.Serialization;

namespace Slack.DTO;

public class ChatPostMessageResponse : SlackResponse
{
    [JsonPropertyName("ts")]
    public string Timestamp { get; set; }
}

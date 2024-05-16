using System.Text.Json.Serialization;

namespace SlackBotManager.Slack;

public class ChatPostMessageResponse : BaseResponse
{
    [JsonPropertyName("ts")]
    public string Timestamp { get; set; }
}

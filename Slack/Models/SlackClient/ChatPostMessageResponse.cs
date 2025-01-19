using System.Text.Json.Serialization;

namespace Slack.Models.SlackClient;

public class ChatPostMessageResponse : BaseResponse
{
    [JsonPropertyName("ts")]
    public string Timestamp { get; set; }
}

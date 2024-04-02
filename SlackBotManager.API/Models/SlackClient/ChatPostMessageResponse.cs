using System.Text.Json.Serialization;

namespace SlackBotManager.API.Models.SlackClient;

public class ChatPostMessageResponse : SlackResponse
{
    [JsonPropertyName("ts")]
    public string TimeStampId { get; set; }
}

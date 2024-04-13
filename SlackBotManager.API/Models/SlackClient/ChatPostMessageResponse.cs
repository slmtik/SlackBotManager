using System.Text.Json.Serialization;

namespace SlackBotManager.API.Models.SlackClient;

public class ChatPostMessageResponse : BaseResponse
{
    [JsonPropertyName("ts")]
    public string Timestamp { get; set; }
}

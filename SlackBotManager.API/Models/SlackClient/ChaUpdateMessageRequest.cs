using SlackBotManager.API.Interfaces;
using System.Text.Json.Serialization;

namespace SlackBotManager.API.Models.SlackClient;

public class ChaUpdateMessageRequest
{
    public string? Text { get; set; }
    public IEnumerable<IBlock>? Blocks { get; set; }

    [JsonPropertyName("channel")]
    public string ChannelId { get; set; }
    public bool? UnfurlLinks { get; set; }
    public Metadata? Metadata { get; set; }
    [JsonPropertyName("ts")]
    public string TimestampId { get; set; }

    public ChaUpdateMessageRequest(string channelId, string timestampId, string text)
    {
        ChannelId = channelId;
        TimestampId = timestampId;
        Text = text;
    }

    public ChaUpdateMessageRequest(string channelId, string timestampId, IEnumerable<IBlock> blocks)
    {
        ChannelId = channelId;
        TimestampId = timestampId;
        Blocks = blocks;
    }
}

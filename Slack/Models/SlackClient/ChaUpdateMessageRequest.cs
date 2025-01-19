using Slack.Interfaces;
using Slack.Models.Payloads;
using System.Text.Json.Serialization;

namespace Slack.Models.SlackClient;

public class ChaUpdateMessageRequest
{
    public string? Text { get; set; }
    public IEnumerable<IBlock>? Blocks { get; set; }

    [JsonPropertyName("channel")]
    public string ChannelId { get; set; }
    public bool? UnfurlLinks { get; set; }
    public Metadata? Metadata { get; set; }
    [JsonPropertyName("ts")]
    public string Timestamp { get; set; }

    public ChaUpdateMessageRequest(string channelId, string timestamp, string text)
    {
        ChannelId = channelId;
        Timestamp = timestamp;
        Text = text;
    }

    public ChaUpdateMessageRequest(string channelId, string timestamp, IEnumerable<IBlock> blocks)
    {
        ChannelId = channelId;
        Timestamp = timestamp;
        Blocks = blocks;
    }
}

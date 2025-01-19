using Slack.Interfaces;
using Slack.Models.Payloads;
using System.Text.Json.Serialization;

namespace Slack.Models.SlackClient;

public class ChatPostMessageRequest
{
    public string? Text { get; set; }
    public IEnumerable<IBlock>? Blocks { get; set; }

    [JsonPropertyName("channel")]
    public string ChannelId { get; set; }
    public bool? UnfurlLinks { get; set; }
    public Metadata? Metadata { get; set; }
    [JsonPropertyName("thread_ts")]
    public string? ThreadTimestamp { get; set; }

    public ChatPostMessageRequest(string channelId, string text)
    {
        ChannelId = channelId;
        Text = text;
    }

    public ChatPostMessageRequest(string channelId, IEnumerable<IBlock> blocks)
    {
        ChannelId = channelId;
        Blocks = blocks;
    }
}

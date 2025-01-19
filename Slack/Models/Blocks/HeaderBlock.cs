using Slack.Interfaces;
using Slack.Models.Elements;
using System.Text.Json.Serialization;

namespace Slack.Models.Blocks;

public class HeaderBlock : IBlock
{
    public PlainText? Text { get; set; }
    public string? BlockId { get; set; }

    [JsonConstructor]
    private HeaderBlock()
    {

    }

    public HeaderBlock(PlainText text)
    {
        Text = text;
    }

    public HeaderBlock(string text) : this(new PlainText(text)) { }
}

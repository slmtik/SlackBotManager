using SlackBotManager.Slack.Elements;
using System.Text.Json.Serialization;

namespace SlackBotManager.Slack.Blocks;

public class SectionBlock : IBlock
{
    public string? BlockId { get; set; }
    public ITextObject? Text { get; set; }
    public IEnumerable<ITextObject>? Fields { get; set; }
    public ISectionElement? Accessory { get; set; }

    [JsonConstructor]
    private SectionBlock() 
    {
        
    }

    public SectionBlock(ITextObject text)
    {
        Text = text;
    }

    public SectionBlock(IEnumerable<ITextObject> fields)
    {
        Fields = fields;
    }

    public SectionBlock(string text) : this(new PlainText(text)) { }
}

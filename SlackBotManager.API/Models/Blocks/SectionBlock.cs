using SlackBotManager.API.Interfaces;
using System.Text.Json.Serialization;

namespace SlackBotManager.API.Models.Blocks;

public class SectionBlock : IBlock
{
    public string? BlockId { get; set; }
    public ITextObject? Text { get; set; }
    public IEnumerable<ITextObject>? Fields { get; set; }
    public IElement? Accessory { get; set; }

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
}

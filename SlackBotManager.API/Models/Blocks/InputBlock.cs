using SlackBotManager.API.Interfaces;
using SlackBotManager.API.Models.Elements;
using System.Text.Json.Serialization;

namespace SlackBotManager.API.Models.Blocks;

public class InputBlock : IBlock
{
    public PlainText? Label { get; set; }
    public IElement? Element { get; set; }
    public string? BlockId { get; set; }
    public bool Optional { get; set; } = false;

    [JsonConstructor]
    private InputBlock()
    {

    }
    public InputBlock(PlainText label, IElement element)
    {
        Label = label;
        Element = element;
    }

    public InputBlock(string label, IElement element) : this(new PlainText(label), element)
    {
        
    }
}

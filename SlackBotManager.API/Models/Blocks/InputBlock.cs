using SlackBotManager.API.Interfaces;
using SlackBotManager.API.Models.Elements;

namespace SlackBotManager.API.Models.Blocks;

public class InputBlock(PlainTextObject label, IElement element) : IBlock
{
    public PlainTextObject Label { get; set; } = label;
    public IElement Element { get; set; } = element;
    public string? BlockId { get; set; }
    public bool Optional { get; set; } = false;

    public InputBlock(string label, IElement element) : this(new PlainTextObject(label), element)
    {
        
    }
}

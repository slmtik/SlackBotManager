using SlackBotManager.Slack.Elements;
using System.Text.Json.Serialization;

namespace SlackBotManager.Slack.Blocks;

public class InputBlock : IBlock
{
    public PlainText? Label { get; set; }
    public IInputElement? Element { get; set; }
    public string? BlockId { get; set; }
    public bool Optional { get; set; } = false;

    [JsonConstructor]
    private InputBlock()
    {

    }
    public InputBlock(PlainText label, IInputElement element)
    {
        Label = label;
        Element = element;
    }

    public InputBlock(string label, IInputElement element) : this(new PlainText(label), element)
    {
        
    }
}

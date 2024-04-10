using SlackBotManager.API.Interfaces;
using System.Text.Json.Serialization;

namespace SlackBotManager.API.Models.Elements;

public class Button(PlainText text) : IElement
{
    public PlainText Text { get; set; } = text;
    public string? ActionId { get; set; }
    public string? Style { get; set; }
    public ButtonConfirm? Confirm { get; set; }

    [JsonConstructor]
    private Button() : this(string.Empty)
    {
        
    }

    public Button(string text) : this(new PlainText(text))
    {
        
    }
}

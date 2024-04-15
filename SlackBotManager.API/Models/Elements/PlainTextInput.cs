using SlackBotManager.API.Interfaces;

namespace SlackBotManager.API.Models.Elements;

public class PlainTextInput : IElement
{
    public string? ActionId { get; set; }
    public string? InitialValue { get; set; }
}

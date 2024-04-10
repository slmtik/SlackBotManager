using SlackBotManager.API.Interfaces;

namespace SlackBotManager.API.Models.Elements;

public class MultiSelectUser : IElement
{
    public string? ActionId { get; set; }
    public string[]? InitialUsers { get; set; }
    public PlainText? Placeholder { get; set; }
}

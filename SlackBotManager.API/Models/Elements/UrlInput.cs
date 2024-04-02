using SlackBotManager.API.Interfaces;

namespace SlackBotManager.API.Models.Elements;

public class UrlInput : IElement
{
    public string? ActionId { get; set; }
}

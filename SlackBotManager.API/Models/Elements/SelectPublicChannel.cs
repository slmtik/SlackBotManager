using SlackBotManager.API.Interfaces;

namespace SlackBotManager.API.Models.Elements;

public class SelectPublicChannel : IElement
{
    public string? ActionId { get; set; }
    public string? InitialChannel { get; set; }
    public PlainText? Placeholder { get; set; }
}

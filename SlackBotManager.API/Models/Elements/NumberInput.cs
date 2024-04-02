using SlackBotManager.API.Interfaces;

namespace SlackBotManager.API.Models.Elements;

public class NumberInput : IElement
{
    public bool IsDecimalAllowed { get; set; }
    public string? ActionId { get; set; }
    public string? InitialValue { get; set; }
    public string? MinValue { get; set; }
    public string? MaxValue { get; set; }
}

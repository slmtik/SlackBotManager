using SlackBotManager.API.Interfaces;

namespace SlackBotManager.API.Models.Blocks;

public class ActionBlock(IEnumerable<IElement> elements) : IBlock
{
    public string? BlockId { get; set; }
    public IEnumerable<IElement> Elements { get; set; } = elements;
}

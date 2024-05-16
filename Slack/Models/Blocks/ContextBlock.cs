using SlackBotManager.Slack.Elements;

namespace SlackBotManager.Slack.Blocks;

public class ContextBlock(IEnumerable<IElement> elements) : IBlock
{
    public string? BlockId { get; set; }
    public IEnumerable<IElement> Elements { get; set; } = elements;
}

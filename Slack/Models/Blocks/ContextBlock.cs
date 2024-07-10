using SlackBotManager.Slack.Elements;

namespace SlackBotManager.Slack.Blocks;

public class ContextBlock(IEnumerable<IContextElement> elements) : IBlock
{
    public string? BlockId { get; set; }
    public IEnumerable<IContextElement> Elements { get; set; } = elements;
}

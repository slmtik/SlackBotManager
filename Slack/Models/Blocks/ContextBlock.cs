using Slack.Interfaces;

namespace Slack.Models.Blocks;

public class ContextBlock(IEnumerable<IContextElement> elements) : IBlock
{
    public string? BlockId { get; set; }
    public IEnumerable<IContextElement> Elements { get; set; } = elements;
}

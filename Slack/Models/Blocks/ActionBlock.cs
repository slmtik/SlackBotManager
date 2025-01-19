using Slack.Interfaces;

namespace Slack.Models.Blocks;

public class ActionBlock(IEnumerable<IActionElement> elements) : IBlock
{
    public string? BlockId { get; set; }
    public IEnumerable<IActionElement> Elements { get; set; } = elements;
}

using SlackBotManager.API.Interfaces;
using System.Xml.Linq;

namespace SlackBotManager.API.Models.Blocks
{
    public class ContextBlock(IEnumerable<IElement> elements) : IBlock
    {
        public string? BlockId { get; set; }
        public IEnumerable<IElement> Elements { get; set; } = elements;
    }
}

using SlackBotManager.API.Interfaces;

namespace SlackBotManager.API.Models.Elements;

public class MultiStaticSelect(IEnumerable<OptionObject<PlainTextObject>> options) : IElement
{
    public IEnumerable<OptionObject<PlainTextObject>> Options { get; set; } = options;
    public IEnumerable<OptionObject<PlainTextObject>>? InitialOptions { get; set; }
    public int? MaxSelectedItems { get; set; }
    public string? ActionId { get; set; }

}

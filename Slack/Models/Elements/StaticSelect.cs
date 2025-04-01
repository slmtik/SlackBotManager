using Slack.Interfaces;

namespace Slack.Models.Elements;

public class StaticSelect(IEnumerable<Option<PlainText>> options) : IInputElement
{
    public IEnumerable<Option<PlainText>> Options { get; set; } = options;
    public Option<PlainText>? InitialOption { get; set; }
    public string? ActionId { get; set; }
}

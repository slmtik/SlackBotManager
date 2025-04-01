using Slack.Interfaces;

namespace Slack.Models.Elements;

public class RadioButton(IEnumerable<Option<PlainText>> options) : ISectionElement
{
    public IEnumerable<Option<PlainText>> Options { get; set; } = options;
    public Option<PlainText>? InitialOption { get; set; }
    public string? ActionId { get; set; }
}

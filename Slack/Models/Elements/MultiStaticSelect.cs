using Slack.Interfaces;

namespace Slack.Models.Elements;

public class MultiStaticSelect(IEnumerable<Option<PlainText>> options) : IInputElement
{
    public IEnumerable<Option<PlainText>> Options { get; set; } = options;
    public IEnumerable<Option<PlainText>>? InitialOptions { get; set; }
    public int? MaxSelectedItems { get; set; }
    public string? ActionId { get; set; }
}

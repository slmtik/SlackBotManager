namespace SlackBotManager.Slack.Elements;

public class MultiStaticSelect(IEnumerable<Option<PlainText>> options) : IElement
{
    public IEnumerable<Option<PlainText>> Options { get; set; } = options;
    public IEnumerable<Option<PlainText>>? InitialOptions { get; set; }
    public int? MaxSelectedItems { get; set; }
    public string? ActionId { get; set; }
}

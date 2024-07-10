using SlackBotManager.Slack.Elements;

namespace Slack.Models.Elements;

public class Checkboxes(IEnumerable<Option<ITextObject>> options) : IInputElement
{
    public string? ActionId { get; set; }
    public IEnumerable<Option<ITextObject>> Options { get; set; } = options;

    public IEnumerable<Option<ITextObject>>? InitialOptions { get; set; }
}

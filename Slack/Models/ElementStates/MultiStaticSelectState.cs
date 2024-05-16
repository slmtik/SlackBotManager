using SlackBotManager.Slack.Elements;

namespace SlackBotManager.Slack.ElementStates;

public class MultiStaticSelectState : IElementState
{
    public IEnumerable<Option<PlainText>>? SelectedOptions { get; set; }
}

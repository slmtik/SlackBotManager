using SlackBotManager.Slack.Elements;

namespace SlackBotManager.Slack.ElementStates;

public class MultiExternalSelectState : IElementState
{
    public IEnumerable<Option<PlainText>>? SelectedOptions { get; set; }
}

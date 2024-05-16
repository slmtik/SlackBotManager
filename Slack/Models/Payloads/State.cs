using SlackBotManager.Slack.ElementStates;

namespace SlackBotManager.Slack.Payloads;

public class State
{
    public Dictionary<string, Dictionary<string, IElementState>> Values { get; set; }
}
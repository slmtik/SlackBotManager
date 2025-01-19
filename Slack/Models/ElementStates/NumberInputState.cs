using Slack.Interfaces;

namespace Slack.Models.ElementStates;

public class NumberInputState : IElementState
{
    public string Value { get; set; } = string.Empty;
}

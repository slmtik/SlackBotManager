using Slack.Interfaces;

namespace Slack.Models.ElementStates;

public class PlainTextInputState : IElementState
{
    public required string Value { get; set; }
}

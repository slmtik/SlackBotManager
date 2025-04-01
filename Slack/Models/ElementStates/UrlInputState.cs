using Slack.Interfaces;

namespace Slack.Models.ElementStates;

public class UrlInputState : IElementState
{
    public string? Value { get; set; }
}

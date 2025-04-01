using Slack.Interfaces;
using Slack.Models.Elements;

namespace Slack.Models.ElementStates;

public class StaticSelectState : IElementState
{
    public Option<PlainText>? SelectedOption { get; set; }
}

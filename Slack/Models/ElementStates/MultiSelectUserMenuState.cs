using Slack.Interfaces;

namespace Slack.Models.ElementStates;

public class MultiSelectUserState : IElementState
{
    public string[]? SelectedUsers { get; set; }
}

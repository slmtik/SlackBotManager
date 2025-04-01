using Slack.Interfaces;

namespace Slack.Models.ElementStates;

public class SelectUserState : IElementState
{
    public required string SelectedUser { get; set; }
}

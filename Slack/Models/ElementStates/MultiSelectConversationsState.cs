using Slack.Interfaces;

namespace Slack.Models.ElementStates;

public class MultiSelectConversationsState : IElementState
{
    public string[]? SelectedConversations { get; set; }
}

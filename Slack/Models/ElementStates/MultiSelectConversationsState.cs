namespace SlackBotManager.Slack.ElementStates;

public class MultiSelectConversationsState : IElementState
{
    public string[]? SelectedConversations { get; set; }
}

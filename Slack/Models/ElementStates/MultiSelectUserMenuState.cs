namespace SlackBotManager.Slack.ElementStates;

public class MultiSelectUserState : IElementState
{
    public string[]? SelectedUsers { get; set; }
}

using SlackBotManager.API.Interfaces;

namespace SlackBotManager.API.Models.ElementStates;

public class MultiSelectConversationsState : IElementState
{
    public string[]? SelectedConversations { get; set; }
}

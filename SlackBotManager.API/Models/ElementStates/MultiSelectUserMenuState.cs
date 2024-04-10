using SlackBotManager.API.Interfaces;

namespace SlackBotManager.API.Models.ElementStates;

public class MultiSelectUserState : IElementState
{
    public string[]? SelectedUsers { get; set; }
}

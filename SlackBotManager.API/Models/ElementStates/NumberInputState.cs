using SlackBotManager.API.Interfaces;

namespace SlackBotManager.API.Models.ElementStates;

public class NumberInputState : IElementState
{
    public string Value { get; set; } = string.Empty;
}

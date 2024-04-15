using SlackBotManager.API.Interfaces;

namespace SlackBotManager.API.Models.ElementStates;

public class PlainTextInputState : IElementState
{
    public required string Value { get; set; }
}

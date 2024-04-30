using SlackBotManager.API.Interfaces;
using SlackBotManager.API.Models.Elements;

namespace SlackBotManager.API.Models.ElementStates;

public class MultiExternalSelectState : IElementState
{
    public IEnumerable<Option<PlainText>>? SelectedOptions { get; set; }
}

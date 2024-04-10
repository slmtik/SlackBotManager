using SlackBotManager.API.Interfaces;
using SlackBotManager.API.Models.Elements;

namespace SlackBotManager.API.Models.ElementStates;

public class MultiStaticSelectState : IElementState
{
    public IEnumerable<Option<PlainText>>? SelectedOptions { get; set; }
}

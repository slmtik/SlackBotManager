using Slack.Interfaces;
using Slack.Models.Elements;

namespace Slack.Models.ElementStates;

public class MultiExternalSelectState : IElementState
{
    public IEnumerable<Option<PlainText>>? SelectedOptions { get; set; }
}

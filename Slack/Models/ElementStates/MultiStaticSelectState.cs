using Slack.Interfaces;
using Slack.Models.Elements;

namespace Slack.Models.ElementStates;

public class MultiStaticSelectState : IElementState
{
    public IEnumerable<Option<PlainText>>? SelectedOptions { get; set; }
}

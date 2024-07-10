using SlackBotManager.Slack.Elements;

namespace Slack.Models.Elements;

public class TimePicker : ISectionElement, IInputElement
{
    public string? ActionId { get; set; }
    public string? InitialTime { get; set; }
}

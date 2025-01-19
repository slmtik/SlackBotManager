using Slack.Interfaces;

namespace Slack.Models.ElementStates;

public class TimePickerState : IElementState
{
    public string SelectedTime { get; set; } = string.Empty;
}

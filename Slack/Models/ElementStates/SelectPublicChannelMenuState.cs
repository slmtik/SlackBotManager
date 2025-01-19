using Slack.Interfaces;

namespace Slack.Models.ElementStates;

public class SelectPublicChannelState : IElementState
{
    public string? SelectedChannel { get; set; }
}

using Slack.Interfaces;

namespace Slack.Models.ElementStates;

public class SelectPublicChannelState : IElementState
{
    public required string SelectedChannel { get; set; }
}

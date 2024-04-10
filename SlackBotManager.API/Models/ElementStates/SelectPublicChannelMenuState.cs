using SlackBotManager.API.Interfaces;

namespace SlackBotManager.API.Models.ElementStates;

public class SelectPublicChannelState : IElementState
{
    public string? SelectedChannel { get; set; }
}

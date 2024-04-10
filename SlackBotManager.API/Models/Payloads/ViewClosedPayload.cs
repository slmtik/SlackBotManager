using SlackBotManager.API.Interfaces;

namespace SlackBotManager.API.Models.Payloads;

public class ViewClosedPayload : IInteractionPayload
{
    public View View { get; set; }
}

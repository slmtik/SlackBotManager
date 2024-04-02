using SlackBotManager.API.Interfaces;

namespace SlackBotManager.API.Models.Payloads;

public class ViewClosedPayload : IPayload
{
    public View View { get; set; }
}

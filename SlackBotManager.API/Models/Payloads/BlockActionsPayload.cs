using SlackBotManager.API.Interfaces;

namespace SlackBotManager.API.Models.Payloads;

public class BlockActionsPayload : IPayload
{
    public Action[] Actions { get; set; }
    public string TriggerId { get; set; }
    public View View { get; set; }
    public Message? Message { get; set; }
    public User User { get; set; }
    public Channel? Channel { get; set; }

}
using SlackBotManager.API.Interfaces;

namespace SlackBotManager.API.Models.Payloads;

public class ViewSubmissionPayload : IInteractionPayload
{
    public View View { get; set; }
    public User User { get; set; }
}
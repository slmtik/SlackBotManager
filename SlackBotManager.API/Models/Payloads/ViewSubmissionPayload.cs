using SlackBotManager.API.Interfaces;

namespace SlackBotManager.API.Models.Payloads;

public class ViewSubmissionPayload : IPayload
{
    public View View { get; set; }
    public User User { get; set; }
}
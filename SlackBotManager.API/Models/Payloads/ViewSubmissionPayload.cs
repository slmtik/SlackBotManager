using SlackBotManager.API.Interfaces;

namespace SlackBotManager.API.Models.Payloads;

public class ViewSubmissionPayload : IInteractionPayload
{
    public View View { get; set; }
    public Enterprise? Enterprise { get; set; }
    public Team? Team { get; set; }
    public User User { get; set; }
    public bool IsEnterpriseInstall { get; set; }
}
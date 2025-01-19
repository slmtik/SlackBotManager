using Slack.Interfaces;
using Slack.Models.Payloads;

namespace Slack.Models.Payloads;

public class ViewClosedPayload : IInteractionPayload
{
    public View View { get; set; }
    public Enterprise? Enterprise { get; set; }
    public Team? Team { get; set; }
    public User User { get; set; }
    public bool IsEnterpriseInstall { get; set; }
}

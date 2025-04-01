using Slack.Interfaces;

namespace Slack.Models.Payloads;

public class BlockActionsPayload : IInteractionPayload
{
    public Action[] Actions { get; set; }
    public required string TriggerId { get; set; }
    public View View { get; set; }
    public Message? Message { get; set; }
    public Channel? Channel { get; set; }
    public Enterprise? Enterprise { get; set; }
    public Team? Team { get; set; }
    public required User User { get; set; }
    public bool IsEnterpriseInstall { get; set; }
}
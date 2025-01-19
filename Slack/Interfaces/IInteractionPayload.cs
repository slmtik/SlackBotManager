using Slack.Models.Payloads;
using System.Text.Json.Serialization;

namespace Slack.Interfaces;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(BlockActionsPayload), typeDiscriminator: "block_actions")]
[JsonDerivedType(typeof(ViewSubmissionPayload), typeDiscriminator: "view_submission")]
[JsonDerivedType(typeof(ViewClosedPayload), typeDiscriminator: "view_closed")]
public interface IInteractionPayload
{
    public Enterprise? Enterprise { get; set; }
    public Team? Team { get; set; }
    public User User { get; set; }
    public bool IsEnterpriseInstall { get; set; }
}

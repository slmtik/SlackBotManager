using System.Text.Json.Serialization;
using SlackBotManager.API.Models.Payloads;

namespace SlackBotManager.API.Interfaces;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(BlockActionsPayload), typeDiscriminator: "block_actions")]
[JsonDerivedType(typeof(ViewSubmissionPayload), typeDiscriminator: "view_submission")]
[JsonDerivedType(typeof(ViewClosedPayload), typeDiscriminator: "view_closed")]
public interface IPayload
{
}

using System.Text.Json.Serialization;

namespace SlackBotManager.Slack;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "response_action")]
[JsonDerivedType(typeof(ErrorSubmissionResponse), typeDiscriminator: "errors")]
public interface ISubmissionResponse
{
}

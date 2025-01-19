using Slack.Models.SlackClient;
using System.Text.Json.Serialization;

namespace Slack.Interfaces;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "response_action")]
[JsonDerivedType(typeof(ErrorSubmissionResponse), typeDiscriminator: "errors")]
public interface ISubmissionResponse
{
}

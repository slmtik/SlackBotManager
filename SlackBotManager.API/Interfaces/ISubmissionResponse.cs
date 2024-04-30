using SlackBotManager.API.Models.SlackClient;
using System.Text.Json.Serialization;

namespace SlackBotManager.API.Interfaces;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "response_action")]
[JsonDerivedType(typeof(ErrorSubmissionResponse), typeDiscriminator: "errors")]
public interface ISubmissionResponse
{
}

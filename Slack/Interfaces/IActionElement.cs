using SlackBotManager.Slack.Elements;
using System.Text.Json.Serialization;

namespace SlackBotManager.Slack.Blocks;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(Button), typeDiscriminator: "button")]
public interface IActionElement : IElement
{
}

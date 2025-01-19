using Slack.Models.Elements;
using System.Text.Json.Serialization;

namespace Slack.Interfaces;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(Button), typeDiscriminator: "button")]
public interface IActionElement : IElement
{
}

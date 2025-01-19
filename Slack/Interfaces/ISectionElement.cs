using Slack.Models.Elements;
using System.Text.Json.Serialization;

namespace Slack.Interfaces;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(Button), typeDiscriminator: "button")]
[JsonDerivedType(typeof(SelectPublicChannel), typeDiscriminator: "channels_select")]
[JsonDerivedType(typeof(MultiSelectConversations), typeDiscriminator: "multi_conversations_select")]
[JsonDerivedType(typeof(TimePicker), typeDiscriminator: "timepicker")]
public interface ISectionElement : IElement
{
}

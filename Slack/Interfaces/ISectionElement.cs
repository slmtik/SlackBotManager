using Slack.Models.Elements;
using Slack.Models.ElementStates;
using System.Text.Json.Serialization;

namespace Slack.Interfaces;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(Button), typeDiscriminator: "button")]
[JsonDerivedType(typeof(SelectPublicChannel), typeDiscriminator: "channels_select")]
[JsonDerivedType(typeof(MultiSelectConversations), typeDiscriminator: "multi_conversations_select")]
[JsonDerivedType(typeof(TimePicker), typeDiscriminator: "timepicker")]
[JsonDerivedType(typeof(RadioButton), typeDiscriminator: "radio_buttons")]
public interface ISectionElement : IElement
{
}

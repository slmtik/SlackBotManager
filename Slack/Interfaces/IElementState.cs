using Slack.Models.ElementStates;
using System.Text.Json.Serialization;

namespace Slack.Interfaces;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(NumberInputState), typeDiscriminator: "number_input")]
[JsonDerivedType(typeof(MultiStaticSelectState), typeDiscriminator: "multi_static_select")]
[JsonDerivedType(typeof(UrlInputState), typeDiscriminator: "url_text_input")]
[JsonDerivedType(typeof(SelectPublicChannelState), typeDiscriminator: "channels_select")]
[JsonDerivedType(typeof(MultiSelectUserState), typeDiscriminator: "multi_users_select")]
[JsonDerivedType(typeof(MultiSelectConversationsState), typeDiscriminator: "multi_conversations_select")]
[JsonDerivedType(typeof(PlainTextInputState), typeDiscriminator: "plain_text_input")]
[JsonDerivedType(typeof(MultiExternalSelectState), typeDiscriminator: "multi_external_select")]
[JsonDerivedType(typeof(TimePickerState), typeDiscriminator: "timepicker")]
[JsonDerivedType(typeof(RadioButtonState), typeDiscriminator: "radio_buttons")]
[JsonDerivedType(typeof(StaticSelectState), typeDiscriminator: "static_select")]
public interface IElementState
{
}

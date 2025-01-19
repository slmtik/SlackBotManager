using Slack.Models.Elements;
using System.Text.Json.Serialization;

namespace Slack.Interfaces;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(UrlInput), typeDiscriminator: "url_text_input")]
[JsonDerivedType(typeof(MultiStaticSelect), typeDiscriminator: "multi_static_select")]
[JsonDerivedType(typeof(NumberInput), typeDiscriminator: "number_input")]
[JsonDerivedType(typeof(PlainTextInput), typeDiscriminator: "plain_text_input")]
[JsonDerivedType(typeof(Checkboxes), typeDiscriminator: "checkboxes")]
[JsonDerivedType(typeof(SelectPublicChannel), typeDiscriminator: "channels_select")]
[JsonDerivedType(typeof(TimePicker), typeDiscriminator: "timepicker")]
public interface IInputElement : IElement
{
    public string? ActionId { get; set; }
}

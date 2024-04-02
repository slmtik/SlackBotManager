using System.Text.Json.Serialization;
using SlackBotManager.API.Models.ElementStates;

namespace SlackBotManager.API.Interfaces;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(NumberInputState), typeDiscriminator: "number_input")]
[JsonDerivedType(typeof(MultiStaticSelectState), typeDiscriminator: "multi_static_select")]
[JsonDerivedType(typeof(UrlInputState), typeDiscriminator: "url_text_input")]
public interface IElementState
{
}

using System.Text.Json.Serialization;
using SlackBotManager.API.Models.Elements;

namespace SlackBotManager.API.Interfaces
{
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(PlainTextObject), typeDiscriminator: "plain_text")]
    [JsonDerivedType(typeof(MarkdownTextObject), typeDiscriminator: "mrkdwn")]
    [JsonDerivedType(typeof(UrlInput), typeDiscriminator: "url_text_input")]
    [JsonDerivedType(typeof(MultiStaticSelect), typeDiscriminator: "multi_static_select")]
    [JsonDerivedType(typeof(Button), typeDiscriminator: "button")]
    [JsonDerivedType(typeof(NumberInput), typeDiscriminator: "number_input")]
    [JsonDerivedType(typeof(Image), typeDiscriminator: "image")]
    public interface IElement
    {
    }
}

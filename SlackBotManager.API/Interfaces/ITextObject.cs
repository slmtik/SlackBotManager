using SlackBotManager.API.Models.Elements;
using System.Text.Json.Serialization;

namespace SlackBotManager.API.Interfaces;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(PlainText), typeDiscriminator: "plain_text")]
[JsonDerivedType(typeof(MarkdownText), typeDiscriminator: "mrkdwn")]
public interface ITextObject
{
    public string Text { get; set; }
}

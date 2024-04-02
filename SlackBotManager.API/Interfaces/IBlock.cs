using System.Text.Json.Serialization;
using SlackBotManager.API.Models.Blocks;

namespace SlackBotManager.API.Interfaces;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(InputBlock), typeDiscriminator: "input")]
[JsonDerivedType(typeof(SectionBlock), typeDiscriminator: "section")]
[JsonDerivedType(typeof(DividerBlock), typeDiscriminator: "divider")]
[JsonDerivedType(typeof(ActionBlock), typeDiscriminator: "actions")]
[JsonDerivedType(typeof(ContextBlock), typeDiscriminator: "context")]
public interface IBlock
{
    public string? BlockId { get; set; }
}

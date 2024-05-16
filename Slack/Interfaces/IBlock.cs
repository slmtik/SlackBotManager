using System.Text.Json.Serialization;

namespace SlackBotManager.Slack.Blocks;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(InputBlock), typeDiscriminator: "input")]
[JsonDerivedType(typeof(SectionBlock), typeDiscriminator: "section")]
[JsonDerivedType(typeof(DividerBlock), typeDiscriminator: "divider")]
[JsonDerivedType(typeof(ActionBlock), typeDiscriminator: "actions")]
[JsonDerivedType(typeof(ContextBlock), typeDiscriminator: "context")]
[JsonDerivedType(typeof(HeaderBlock), typeDiscriminator: "header")]
public interface IBlock
{
    public string? BlockId { get; set; }
}

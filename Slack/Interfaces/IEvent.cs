using Slack.Models.Events;
using System.Text.Json.Serialization;

namespace Slack.Interfaces;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(AppHomeOpenedEvent), typeDiscriminator: "app_home_opened")]
[JsonDerivedType(typeof(MessageChannelsEvent), typeDiscriminator: "message")]
public interface IEvent
{
    public string Type { get; }
    public string? User { get; set; }
}

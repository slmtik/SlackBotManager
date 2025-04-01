using Slack.Interfaces;

namespace Slack.Models.Events;

public class MessageChannelsEvent : IEvent
{
    public string Type => "message";
    public string? SubType { get; set; }
    public string? User { get; set; }
    public required string Channel { get; set; }
    public string? Text { get; set; }
    public IEnumerable<Attachment>? Attachments { get; set; }
}

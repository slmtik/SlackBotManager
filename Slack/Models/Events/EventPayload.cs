using Slack.Interfaces;

namespace Slack.Models.Events;

public class EventPayload
{
    public required IEvent Event { get; set; }
}
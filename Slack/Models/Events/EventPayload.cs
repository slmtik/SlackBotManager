namespace SlackBotManager.Slack.Events;

public class EventPayload
{
    public Event? Event { get; set; }
    public string? Type { get; set; }
    public string? Challenge { get; set; }
    public Authorization[]? Authorizations { get; set; }
}
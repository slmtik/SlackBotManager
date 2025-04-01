using Slack.Interfaces;

namespace Slack.Models.Events;

public class AppHomeOpenedEvent : IEvent
{
    public string Type => "app_home_opened";
    public required string User { get; set; }
}

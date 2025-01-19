using Slack.Interfaces;

namespace Slack.Models.Elements;

public class MultiSelectConversations : ISectionElement
{
    public string? ActionId { get; set; }
    public string[]? InitialConversations { get; set; }
    public PlainText? Placeholder { get; set; }

    public Filter? Filter { get; set; }
}

using Slack.Interfaces;

namespace Slack.Models.Elements;

public class SelectUser : IElement
{
    public string? ActionId { get; set; }
    public string? InitialUser { get; set; }
    public PlainText? Placeholder { get; set; }
}

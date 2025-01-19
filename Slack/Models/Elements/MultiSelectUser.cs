using Slack.Interfaces;

namespace Slack.Models.Elements;

public class MultiSelectUser : IElement
{
    public string? ActionId { get; set; }
    public string[]? InitialUsers { get; set; }
    public PlainText? Placeholder { get; set; }
}

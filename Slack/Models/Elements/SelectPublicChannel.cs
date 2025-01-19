using Slack.Interfaces;

namespace Slack.Models.Elements;

public class SelectPublicChannel : ISectionElement, IInputElement
{
    public string? ActionId { get; set; }
    public string? InitialChannel { get; set; }
    public PlainText? Placeholder { get; set; }
}

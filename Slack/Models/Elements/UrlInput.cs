using Slack.Interfaces;

namespace Slack.Models.Elements;

public class UrlInput : IInputElement
{
    public string? ActionId { get; set; }
    public string? InitialValue { get; set; }
}

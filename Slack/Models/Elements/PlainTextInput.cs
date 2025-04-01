using Slack.Interfaces;

namespace Slack.Models.Elements;

public class PlainTextInput : IInputElement
{
    public string? ActionId { get; set; }
    public string? InitialValue { get; set; }
    public bool? Multiline { get; set; }
}

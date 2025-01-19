using Slack.Interfaces;

namespace Slack.Models.Elements;

public class Option<T>(T text, string value) where T : ITextObject
{
    public T Text { get; set; } = text;
    public string Value { get; set; } = value;
}

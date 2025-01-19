using Slack.Interfaces;

namespace Slack.Models.Elements;

public class MarkdownText(string text) : IContextElement, ITextObject
{
    public string Text { get; set; } = text;
}

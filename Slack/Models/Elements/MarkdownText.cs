namespace SlackBotManager.Slack.Elements;

public class MarkdownText(string text) : IContextElement, ITextObject
{
    public string Text { get; set; } = text;
}

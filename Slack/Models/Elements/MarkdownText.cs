namespace SlackBotManager.Slack.Elements;

public class MarkdownText(string text) : IElement, ITextObject
{
    public string Text { get; set; } = text;
}

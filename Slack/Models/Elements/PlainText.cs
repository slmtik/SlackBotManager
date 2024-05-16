namespace SlackBotManager.Slack.Elements;

public class PlainText(string text) : IElement, ITextObject
{
    public string Type { get; } = "plain_text";
    public string Text { get; set; } = text;
}

namespace SlackBotManager.Slack.Elements;

public class Option<T>(T text, string value) where T : ITextObject
{
    public T Text { get; set; } = text;
    public string Value { get; set; } = value;
}

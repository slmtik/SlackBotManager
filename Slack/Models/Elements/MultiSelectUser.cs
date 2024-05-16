namespace SlackBotManager.Slack.Elements;

public class MultiSelectUser : IElement
{
    public string? ActionId { get; set; }
    public string[]? InitialUsers { get; set; }
    public PlainText? Placeholder { get; set; }
}

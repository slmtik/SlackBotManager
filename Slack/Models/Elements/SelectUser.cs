namespace SlackBotManager.Slack.Elements;

public class SelectUser : IElement
{
    public string? ActionId { get; set; }
    public string? InitialUser { get; set; }
    public PlainText? Placeholder { get; set; }
}

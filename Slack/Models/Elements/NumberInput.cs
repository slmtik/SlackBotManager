namespace SlackBotManager.Slack.Elements;

public class NumberInput : IInputElement
{
    public bool IsDecimalAllowed { get; set; }
    public string? ActionId { get; set; }
    public string? InitialValue { get; set; }
    public string? MinValue { get; set; }
    public string? MaxValue { get; set; }
}

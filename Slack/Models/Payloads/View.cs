using SlackBotManager.Slack.Blocks;

namespace SlackBotManager.Slack.Payloads;

public class View
{
    public string PrivateMetadata { get; set; }
    public State State { get; set; }
    public string RootViewId { get; set; }
    public string ViewCallBackId { get; set; }
    public string CallbackId { get; set; }
    public string Type { get; set; }
    public IBlock[]? Blocks { get; set; }

}
using SlackBotManager.Slack.Blocks;

namespace SlackBotManager.Slack.Views;

public interface IView
{
    public string Type { get; }
    public IEnumerable<IBlock> Blocks { get; set; }
}

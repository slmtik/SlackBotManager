using SlackBotManager.Slack.Blocks;
using SlackBotManager.Slack.Elements;

namespace SlackBotManager.Slack.Views;

public class ModalView(PlainText title, IEnumerable<IBlock> blocks) : IView
{
    public string Type { get; } = "modal";
    public PlainText Title { get; set; } = title;
    public IEnumerable<IBlock> Blocks { get; set; } = blocks;
    public string? CallbackId { get; set; }
    public PlainText? Submit { get; set; }
    public string? PrivateMetadata { get; set; }
    public bool NotifyOnClose { get; set; } = false;

    public ModalView(string title, IEnumerable<IBlock> blocks) : this(new PlainText(title), blocks)
    {
        
    }
}

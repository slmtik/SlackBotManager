namespace Slack.Interfaces;

public interface IView
{
    public string Type { get; }
    public IEnumerable<IBlock> Blocks { get; set; }
}

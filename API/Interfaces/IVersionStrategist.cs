using Slack.Interfaces;

namespace API.Interfaces;

public interface IVersionStrategist
{
    public string Name { get; }
    public string Description { get; }
    public ICollection<IBlock> GetBlocks(Dictionary<string, string> values, Func<string, string>? blockIdNaming = null);
    public Dictionary<string, string> ToDictionary(Dictionary<string, IElementState> elementStates);
    public Task<string> GetVersion(Dictionary<string, string> values);
}

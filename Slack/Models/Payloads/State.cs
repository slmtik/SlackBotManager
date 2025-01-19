using Slack.Interfaces;

namespace Slack.Models.Payloads;

public class State
{
    public Dictionary<string, Dictionary<string, IElementState>> Values { get; set; }
}
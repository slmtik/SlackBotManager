using SlackBotManager.API.Interfaces;

namespace SlackBotManager.API.Models.Payloads;

public class State
{
    public Dictionary<string, Dictionary<string, IElementState>> Values { get; set; }
}
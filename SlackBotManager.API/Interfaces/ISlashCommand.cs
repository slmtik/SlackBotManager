using SlackBotManager.API.Models.SlackClient;
using SlackBotManager.API.Services;

namespace SlackBotManager.API.Interfaces
{
    public interface ISlashCommand : IInteraction
    {
        public Dictionary<string, Func<SlackClient, CommandRequest, Task>> CommandBindings { get; }
    }
}

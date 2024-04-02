using SlackBotManager.API.Models.Payloads;
using SlackBotManager.API.Services;

namespace SlackBotManager.API.Interfaces
{
    public interface IViewClosedInteraction : IInteraction
    {
        public Dictionary<string, Func<SlackClient, ViewClosedPayload, Task>> ViewClosedBindings { get; }
    }
}

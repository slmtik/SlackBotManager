using SlackBotManager.API.Interfaces.Invocations;
using SlackBotManager.API.Models.Events;
using SlackBotManager.API.Services;

namespace SlackBotManager.API.Interfaces;

public interface IEventInvocation : IInvocation
{
    public Dictionary<string, Func<SlackClient, EventPayload, Task>> EventBindings { get; }
}

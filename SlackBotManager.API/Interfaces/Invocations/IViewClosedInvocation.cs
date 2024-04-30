using SlackBotManager.API.Interfaces.Invocations;
using SlackBotManager.API.Models.Payloads;
using SlackBotManager.API.Services;

namespace SlackBotManager.API.Interfaces;

public interface IViewClosedInvocation : IInvocation
{
    public Dictionary<string, Func<SlackClient, ViewClosedPayload, Task<IRequestResult>>> ViewClosedBindings { get; }
}

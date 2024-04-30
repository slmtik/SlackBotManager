using SlackBotManager.API.Interfaces.Invocations;
using SlackBotManager.API.Models.Payloads;
using SlackBotManager.API.Services;

namespace SlackBotManager.API.Interfaces;

public interface IBlockActionsInvocation : IInvocation
{
    public Dictionary<(string BlockId, string ActionId), Func<SlackClient, BlockActionsPayload, Task<IRequestResult>>> BlockActionsBindings { get; }
}

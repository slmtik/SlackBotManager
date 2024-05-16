using SlackBotManager.Slack.Payloads;
using SlackBotManager.Slack;

namespace SlackBotManager.API.Invocations;

public interface IBlockActionsInvocation : IInvocation
{
    public Dictionary<(string BlockId, string ActionId), Func<SlackClient, BlockActionsPayload, Task<IRequestResult>>> BlockActionsBindings { get; }
}

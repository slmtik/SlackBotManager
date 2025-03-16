using Core.ApiClient;
using Slack;
using Slack.Models.Payloads;

namespace API.Interfaces.Invocations;

public interface IBlockActionsInvocation : IInvocation
{
    public Dictionary<(string BlockId, string ActionId), Func<SlackClient, BlockActionsPayload, Task<IRequestResult>>> BlockActionsBindings { get; }
}

using Core.ApiClient;
using Slack.Models.Payloads;

namespace API.Interfaces.Invocations;

public interface IBlockActionsInvocation : IInvocation
{
    public Dictionary<(string? BlockId, string? ActionId), Func<BlockActionsPayload, Task<IRequestResult>>> BlockActionsBindings { get; }
}

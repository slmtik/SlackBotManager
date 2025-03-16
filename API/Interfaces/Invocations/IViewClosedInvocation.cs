using Core.ApiClient;
using Slack;
using Slack.Models.Payloads;

namespace API.Interfaces.Invocations;

public interface IViewClosedInvocation : IInvocation
{
    public Dictionary<string, Func<SlackClient, ViewClosedPayload, Task<IRequestResult>>> ViewClosedBindings { get; }
}

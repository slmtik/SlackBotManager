using Slack;
using Slack.Interfaces;
using Slack.Models.Payloads;

namespace API.Interfaces.Invocations;

public interface IViewClosedInvocation : IInvocation
{
    public Dictionary<string, Func<SlackClient, ViewClosedPayload, Task<IRequestResult>>> ViewClosedBindings { get; }
}

using SlackBotManager.Slack.Payloads;
using SlackBotManager.Slack;

namespace SlackBotManager.API.Invocations;

public interface IViewClosedInvocation : IInvocation
{
    public Dictionary<string, Func<SlackClient, ViewClosedPayload, Task<IRequestResult>>> ViewClosedBindings { get; }
}

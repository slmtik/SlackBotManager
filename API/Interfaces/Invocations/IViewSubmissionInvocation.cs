using SlackBotManager.Slack;
using SlackBotManager.Slack.Payloads;

namespace SlackBotManager.API.Invocations;

public interface IViewSubmissionInvocation : IInvocation
{
    public Dictionary<string, Func<SlackClient, ViewSubmissionPayload, Task<IRequestResult>>> ViewSubmissionBindings { get; }
}

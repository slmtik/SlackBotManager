using Slack;
using Slack.Interfaces;
using Slack.Models.Payloads;

namespace API.Interfaces.Invocations;

public interface IViewSubmissionInvocation : IInvocation
{
    public Dictionary<string, Func<SlackClient, ViewSubmissionPayload, Task<IRequestResult>>> ViewSubmissionBindings { get; }
}

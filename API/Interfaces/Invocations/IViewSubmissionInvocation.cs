using Core.ApiClient;
using Slack;
using Slack.Models.Payloads;

namespace API.Interfaces.Invocations;

public interface IViewSubmissionInvocation : IInvocation
{
    public Dictionary<string, Func<SlackClient, ViewSubmissionPayload, Task<IRequestResult>>> ViewSubmissionBindings { get; }
}

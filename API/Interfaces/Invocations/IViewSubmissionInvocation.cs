using Core.ApiClient;
using Slack.Models.Payloads;

namespace API.Interfaces.Invocations;

public interface IViewSubmissionInvocation : IInvocation
{
    public Dictionary<string, Func<ViewSubmissionPayload, Task<IRequestResult>>> ViewSubmissionBindings { get; }
}

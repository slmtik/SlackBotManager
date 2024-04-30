using SlackBotManager.API.Interfaces.Invocations;
using SlackBotManager.API.Models.Payloads;
using SlackBotManager.API.Services;

namespace SlackBotManager.API.Interfaces;

public interface IViewSubmissionInvocation : IInvocation
{
    public Dictionary<string, Func<SlackClient, ViewSubmissionPayload, Task<IRequestResult>>> ViewSubmissionBindings { get; }
}

using SlackBotManager.API.Models.Payloads;
using SlackBotManager.API.Services;

namespace SlackBotManager.API.Interfaces;

public interface IViewSubmissionInvocation
{
    public Dictionary<string, Func<SlackClient, ViewSubmissionPayload, Task>> ViewSubmissionBindings { get; }
}

using Core.ApiClient;
using Slack.Models.Events;

namespace API.Interfaces.Invocations;

public interface IEventInvocation : IInvocation
{
    public Dictionary<string, Func<EventPayload, Task<IRequestResult>>> EventBindings { get; }
}

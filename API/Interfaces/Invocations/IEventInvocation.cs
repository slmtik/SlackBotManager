using Slack;
using Slack.Models.Events;

namespace API.Interfaces.Invocations;

public interface IEventInvocation : IInvocation
{
    public Dictionary<string, Func<SlackClient, EventPayload, Task>> EventBindings { get; }
}

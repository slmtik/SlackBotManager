using SlackBotManager.Slack.Events;
using SlackBotManager.Slack;

namespace SlackBotManager.API.Invocations;

public interface IEventInvocation : IInvocation
{
    public Dictionary<string, Func<SlackClient, EventPayload, Task>> EventBindings { get; }
}

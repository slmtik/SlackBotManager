using SlackBotManager.Slack.Commands;
using SlackBotManager.Slack;

namespace SlackBotManager.API.Invocations;

public interface ICommandInvocation : IInvocation
{
    public Dictionary<string, Func<SlackClient, Command, Task<IRequestResult>>> CommandBindings { get; }
}

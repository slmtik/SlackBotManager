using Slack;
using Slack.Interfaces;
using Slack.Models.Commands;

namespace API.Interfaces.Invocations;

public interface ICommandInvocation : IInvocation
{
    public Dictionary<string, Func<SlackClient, Command, Task<IRequestResult>>> CommandBindings { get; }
}

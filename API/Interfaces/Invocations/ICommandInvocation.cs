using Core.ApiClient;
using Slack;
using Slack.Models.Commands;

namespace API.Interfaces.Invocations;

public interface ICommandInvocation : IInvocation
{
    public Dictionary<string, Func<SlackClient, Command, Task<IRequestResult>>> CommandBindings { get; }
}

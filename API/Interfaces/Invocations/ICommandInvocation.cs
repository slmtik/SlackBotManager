using Core.ApiClient;
using Slack.Models.Commands;

namespace API.Interfaces.Invocations;

public interface ICommandInvocation : IInvocation
{
    public Dictionary<string, Func<Command, Task<IRequestResult>>> CommandBindings { get; }
}

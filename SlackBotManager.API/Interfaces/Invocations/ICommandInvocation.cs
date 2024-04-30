using SlackBotManager.API.Interfaces.Invocations;
using SlackBotManager.API.Models.Commands;
using SlackBotManager.API.Services;

namespace SlackBotManager.API.Interfaces;

public interface ICommandInvocation : IInvocation
{
    public Dictionary<string, Func<SlackClient, Command, Task<IRequestResult>>> CommandBindings { get; }
}

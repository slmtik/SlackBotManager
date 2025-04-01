using Core.ApiClient;
using Slack.Models.Payloads;

namespace API.Interfaces.Invocations;

public interface IViewClosedInvocation : IInvocation
{
    public Dictionary<string, Func<ViewClosedPayload, Task<IRequestResult>>> ViewClosedBindings { get; }
}

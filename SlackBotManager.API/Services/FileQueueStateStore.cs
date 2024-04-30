using SlackBotManager.API.Interfaces.Stores;
using SlackBotManager.API.Models.Core;
using SlackBotManager.API.Models.Stores;

namespace SlackBotManager.API.Services;

public class FileQueueStateStore(IConfiguration configuration, IHttpContextAccessor httpContextAccessor) : 
    FileStoreBase<QueueState>(configuration, httpContextAccessor), IQueueStateStore
{
    protected override string ConfigurationSection => "Slack:QueueStoreLocation";
    protected override string ConfigurationFolder => ".queue";
    protected override string ConfigurationFile => "queue";
}

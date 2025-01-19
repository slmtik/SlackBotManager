using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Persistence.Interfaces;
using Persistence.Models;

namespace Persistence.FileStores;

public class FileQueueStateStore(IConfiguration configuration, IHttpContextAccessor httpContextAccessor) :
    FileStoreBase<QueueState>(configuration, httpContextAccessor), IQueueStateStore
{
    protected override string ConfigurationSection => "Slack:QueueStoreLocation";
    protected override string ConfigurationFolder => ".queue";
    protected override string ConfigurationFile => "queue";
}

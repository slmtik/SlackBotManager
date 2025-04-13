using Core;
using Microsoft.Extensions.Configuration;
using Persistence.Interfaces;
using Persistence.Models;

namespace Persistence.FileStores;

public class FileQueueStateStore(IConfiguration configuration, RequestContext requestContext) :
    FileStoreBase<QueueState>(configuration, requestContext), IQueueStateStore
{
    protected override string ConfigurationSection => "Slack:QueueStoreLocation";
    protected override string ConfigurationFolder => ".queue";
    protected override string ConfigurationFile => "queue";
}

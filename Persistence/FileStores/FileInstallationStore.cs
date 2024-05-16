using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using SlackBotManager.Persistence.Models;

namespace SlackBotManager.Persistence.FileStores;

public class FileInstallationStore(IConfiguration configuration, IHttpContextAccessor httpContextAccessor) :
    FileStoreBase<Installation>(configuration, httpContextAccessor), IInstallationStore
{
    protected override string ConfigurationSection => "Slack:installationStoreLocation";
    protected override string ConfigurationFolder => ".installation";
    protected override string ConfigurationFile => "installer";
}

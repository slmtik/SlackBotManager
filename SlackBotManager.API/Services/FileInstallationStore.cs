using SlackBotManager.API.Interfaces.Stores;
using SlackBotManager.API.Models.Core;
using SlackBotManager.API.Models.Stores;

namespace SlackBotManager.API.Services;

public class FileInstallationStore(IConfiguration configuration, IHttpContextAccessor httpContextAccessor) : 
    FileStoreBase<Installation>(configuration, httpContextAccessor), IInstallationStore
{
    protected override string ConfigurationSection => "Slack:installationStoreLocation";
    protected override string ConfigurationFolder => ".installation";
    protected override string ConfigurationFile => "installer";
}

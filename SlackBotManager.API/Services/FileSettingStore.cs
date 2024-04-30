using SlackBotManager.API.Interfaces.Stores;
using SlackBotManager.API.Models.Core;
using SlackBotManager.API.Models.Stores;

namespace SlackBotManager.API.Services;

public class FileSettingStore(IConfiguration configuration, IHttpContextAccessor httpContextAccessor) : 
    FileStoreBase<Setting>(configuration, httpContextAccessor), ISettingStore
{
    protected override string ConfigurationSection => "Slack:SettingStoreLocation";
    protected override string ConfigurationFolder => ".setting";
    protected override string ConfigurationFile => "setting";
}

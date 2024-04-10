using SlackBotManager.API.Models.Stores;

namespace SlackBotManager.API.Interfaces;

public interface ISettingStore
{
    public Setting? FindSetting(string? enterpriseId, string? teamId, bool? isEnterpriseInstall);
    public void Save(Setting setting);
}

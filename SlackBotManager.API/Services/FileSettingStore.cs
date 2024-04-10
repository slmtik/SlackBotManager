using SlackBotManager.API.Interfaces;
using SlackBotManager.API.Models.Stores;
using System.Text.Json;

namespace SlackBotManager.API.Services;

public class FileSettingStore(IConfiguration configuration) : ISettingStore
{
    private const string _placeholder = "none";

    private readonly string _directory = configuration["Slack:InstallationStoreLocation"] ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SlackBotManager", ".setting");

    public Setting? FindSetting(string? enterpriseId, string? teamId, bool? isEnterpriseInstall)
    {
        enterpriseId ??= _placeholder;
        teamId = teamId is null || (isEnterpriseInstall ?? false) ? _placeholder : teamId;

        string settingFilePath = Path.Combine(_directory, $"{enterpriseId}-{teamId}", "setting-latest");

        Setting? setting = null;
        if (!File.Exists(settingFilePath))
            return setting;

        using (var reader = new StreamReader(settingFilePath))
        {
            var content = reader.ReadToEnd();
            setting = JsonSerializer.Deserialize<Setting>(content);
        }

        return setting;
    }

    public void Save(Setting setting)
    {
        var enterpriseId = setting.EnterpriseId ?? _placeholder;
        var teamId = setting.TeamId ?? _placeholder;

        var settingFilePath = Path.Combine(_directory, $"{enterpriseId}-{teamId}");
        Directory.CreateDirectory(settingFilePath);

        var installerFilePath = Path.Combine(settingFilePath, $"setting-latest");
        using var writer = new StreamWriter(installerFilePath);
        var content = JsonSerializer.Serialize(setting);
        writer.Write(content);
    }
}

using SlackBotManager.API.Interfaces;
using SlackBotManager.API.Models.Repositories;
using System.Text.Json;

namespace SlackBotManager.API.Services;

public class FileSettingRepository(IConfiguration configuration) : ISettingRepository
{
    private const string _placeholder = "none";

    private readonly string _directory = configuration["Slack:InstallationStoreLocation"] ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "SlackBotManager", ".setting");

    public async Task<Setting?> Find(string? enterpriseId, string? teamId, string? userId, bool? isEnterpriseInstall)
    {
        enterpriseId ??= _placeholder;
        teamId = teamId is null || (isEnterpriseInstall ?? false) ? _placeholder : teamId;

        string settingFilePath = Path.Combine(_directory, $"{enterpriseId}-{teamId}", "setting-latest");

        Setting? setting = null;
        if (!File.Exists(settingFilePath))
            return setting;

        using var reader = new StreamReader(settingFilePath);
        var content = await reader.ReadToEndAsync();
        return JsonSerializer.Deserialize<Setting>(content);
    }

    public async Task Save(Setting setting)
    {
        var enterpriseId = setting.EnterpriseId ?? _placeholder;
        var teamId = setting.TeamId ?? _placeholder;

        var settingFilePath = Path.Combine(_directory, $"{enterpriseId}-{teamId}");
        Directory.CreateDirectory(settingFilePath);

        var installerFilePath = Path.Combine(settingFilePath, $"setting-latest");
        using var writer = new StreamWriter(installerFilePath);
        var content = JsonSerializer.Serialize(setting);
        await writer.WriteAsync(content);
    }
}

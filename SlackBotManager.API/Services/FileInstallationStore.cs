using SlackBotManager.API.Interfaces;
using SlackBotManager.API.Models.Stores;
using System.Text.Json;

namespace SlackBotManager.API.Services;

public class FileInstallationStore(IConfiguration configuration) : IInstallationStore
{
    private const string _placeholder = "none";

    private readonly string _directory = configuration["Slack:InstallationStoreLocation"] ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SlackBotManager", ".installation");

    public Bot? FindBot(string? enterpriseId, string? teamId, bool? isEnterpriseInstall)
    {
        enterpriseId ??= _placeholder;
        teamId = teamId is null || (isEnterpriseInstall ?? false) ? _placeholder : teamId;

        var botFilePath = Path.Combine(_directory, $"{enterpriseId}-{teamId}", "bot-latest");
        Bot? bot = null;
        if (!File.Exists(botFilePath))
            return bot;

        using var reader = new StreamReader(botFilePath);
        var content = reader.ReadToEnd();
        bot = JsonSerializer.Deserialize<Bot>(content);

        return bot;
    }

    public Installation? FindInstallation(string? enterpriseId, string? teamId, string? userId, bool? isEnterpriseInstall)
    {
        enterpriseId ??= _placeholder;
        teamId = teamId is null || (isEnterpriseInstall ?? false) ? _placeholder : teamId;

        string instalationFilePath;
        if (userId == null)
            instalationFilePath = Path.Combine(_directory, $"{enterpriseId}-{teamId}", "installer-latest");
        else
            instalationFilePath = Path.Combine(_directory, $"{enterpriseId}-{teamId}", $"installer-{userId}-latest");

        Installation? installation = null;
        if (!File.Exists(instalationFilePath))
            return installation;

        using (var reader = new StreamReader(instalationFilePath))
        {
            var content = reader.ReadToEnd();
            installation = JsonSerializer.Deserialize<Installation>(content);
        }

        if (installation != null && userId != null)
        {
            Installation? latestBotInstallation = FindInstallation(enterpriseId, teamId, null, isEnterpriseInstall);

            if (latestBotInstallation != null && installation.BotToken!.Equals(latestBotInstallation.BotToken))
            {
                installation.BotId = latestBotInstallation.BotId;
                installation.BotRefreshToken = latestBotInstallation.BotRefreshToken;
                installation.BotScopes = latestBotInstallation.BotScopes;
                installation.BotToken = latestBotInstallation.BotToken;
                installation.BotTokenExpiresAt = latestBotInstallation.BotTokenExpiresAt;
                installation.BotUserId = latestBotInstallation.BotUserId;
            }
        }

        return installation;
    }

    public void Save(Installation installation)
    {
        var enterpriseId = installation.EnterpriseId ?? _placeholder;
        var teamId = installation.TeamId ?? _placeholder;
        var userId = installation.UserId ?? _placeholder;

        var teamInstallationDir = Path.Combine(_directory, $"{enterpriseId}-{teamId}");
        Directory.CreateDirectory(teamInstallationDir);

        SaveBot(installation.ToBot());

        var installerFilePath = Path.Combine(teamInstallationDir, $"installer-latest");
        using (var writer = new StreamWriter(installerFilePath))
        {
            var content = JsonSerializer.Serialize(installation);
            writer.Write(content);
        }

        installerFilePath = Path.Combine(teamInstallationDir, $"installer-{userId}-latest");
        using (var writer = new StreamWriter(installerFilePath))
        {
            var content = JsonSerializer.Serialize(installation);
            writer.Write(content);
        }
    }

    private void SaveBot(Bot bot)
    {
        var enterpriseId = bot.EnterpriseId ?? _placeholder;
        var teamId = bot.TeamId ?? _placeholder;

        var teamInstallationDir = Path.Combine(_directory, $"{enterpriseId}-{teamId}");
        Directory.CreateDirectory(teamInstallationDir);

        using var writer = new StreamWriter(Path.Combine(teamInstallationDir, "bot-latest"));
        var content = JsonSerializer.Serialize(bot);
        writer.Write(content);
    }
}

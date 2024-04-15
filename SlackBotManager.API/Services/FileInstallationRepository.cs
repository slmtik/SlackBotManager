﻿using SlackBotManager.API.Interfaces;
using SlackBotManager.API.Models.Repositories;
using System.Text.Json;

namespace SlackBotManager.API.Services;

public class FileInstallationRepository(IConfiguration configuration) : IInstallationRepository
{
    private const string _placeholder = "none";

    private readonly string _directory = configuration["Slack:InstallationStoreLocation"] ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "SlackBotManager", ".installation");

    public async Task<Bot?> FindBot(string? enterpriseId, string? teamId, bool? isEnterpriseInstall)
    {
        enterpriseId ??= _placeholder;
        teamId = teamId is null || (isEnterpriseInstall ?? false) ? _placeholder : teamId;

        var botFilePath = Path.Combine(_directory, $"{enterpriseId}-{teamId}", "bot-latest");
        Bot? bot = null;
        if (!File.Exists(botFilePath))
            return bot;

        using var reader = new StreamReader(botFilePath);
        var content = await reader.ReadToEndAsync();
        return JsonSerializer.Deserialize<Bot>(content);
    }

    public async Task<Installation?> Find(string? enterpriseId, string? teamId, string? userId, bool? isEnterpriseInstall)
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
            var content = await reader.ReadToEndAsync();
            installation = JsonSerializer.Deserialize<Installation>(content);
        }

        if (installation != null && userId != null)
        {
            Installation? latestBotInstallation = await Find(enterpriseId, teamId, null, isEnterpriseInstall);

            if (latestBotInstallation != null && installation.BotToken.Equals(latestBotInstallation.BotToken))
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

    public async Task Save(Installation installation)
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
            await writer.WriteAsync(content);
        }

        installerFilePath = Path.Combine(teamInstallationDir, $"installer-{userId}-latest");
        using (var writer = new StreamWriter(installerFilePath))
        {
            var content = JsonSerializer.Serialize(installation);
            await writer.WriteAsync(content);
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

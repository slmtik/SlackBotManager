using SlackBotManager.API.Interfaces;
using SlackBotManager.API.Models.OAuth;
using SlackBotManager.API.Services;
using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace SlackBotManager.API.MIddlewares;

public class SlackTokenRotator(RequestDelegate next)
{
    private static readonly string[] _slackPaths = ["/api/slack/commands", "/api/slack/events"];

    private readonly RequestDelegate _next = next;

    public async Task InvokeAsync(HttpContext context, IInstallationStore installationStore, SlackClient slackClient)
    {
        if (_slackPaths.Contains(context.Request.Path.ToString()))
        {
            string rawBody = string.Empty;

            ReadResult readResult = await context.Request.BodyReader.ReadAsync();
            var buffer = readResult.Buffer;

            if (readResult.IsCompleted && buffer.Length > 0)
                rawBody = Encoding.UTF8.GetString(buffer.IsSingleSegment ? buffer.FirstSpan : buffer.ToArray().AsSpan());

            context.Request.BodyReader.AdvanceTo(buffer.Start, buffer.End);

            var (teamId, userId, enterpriseId, isEnterpriseInstall) = ParseBody(rawBody);

            await RotateToken(installationStore, slackClient, teamId, userId, enterpriseId, isEnterpriseInstall);

            Bot? bot = installationStore.FindBot(enterpriseId, teamId, isEnterpriseInstall);

            if (string.IsNullOrEmpty(bot?.BotToken))
            {
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                await context.Response.WriteAsync("Please install this app first!");
                return;
            }

            context.Items["bot-token"] = bot?.BotToken;
        }

        await _next(context);
    }

    
    private static (string?, string?, string?, bool) ParseBody(string rawBody)
    {
        string? teamId;
        string? userId;
        string? enterpriseId = null;
        bool isEnterpriseInstall;

        var body = new Microsoft.AspNetCore.WebUtilities.FormReader(rawBody).ReadForm();
        if (body.TryGetValue("payload", out var payload))
        {
            var jsonPayload = JsonNode.Parse(payload!)!;
            teamId = (jsonPayload["team_id"] ?? jsonPayload["team"]?["id"] ?? jsonPayload["team"] ?? jsonPayload["user"]?["team_id"])?.ToString();
            userId = (jsonPayload["user_id"] ?? jsonPayload["user"]?["id"] ?? jsonPayload["user"])?.ToString();
            enterpriseId = (jsonPayload["enterprise_id"] ?? jsonPayload["enterprise"]?["id"] ?? jsonPayload["enterprise"])?.ToString();
            isEnterpriseInstall = Convert.ToBoolean(jsonPayload["is_enterprise_install"]?.ToString());
        }
        else
        {
            teamId = body["team_id"];
            userId = body["user_id"];
            if (body.TryGetValue("enterprise_id", out _)) { enterpriseId = body["enterprise_id"]; }
            isEnterpriseInstall = Convert.ToBoolean(body["is_enterprise_install"]);
        }
        return (teamId, userId, enterpriseId, isEnterpriseInstall);
    }

    private static async Task RotateToken(IInstallationStore installationStore,
                                   SlackClient slackClient,
                                   string? teamId,
                                   string? userId,
                                   string? enterpriseId,
                                   bool isEnterpriseInstall)
    {
        var installation = installationStore.FindInstallation(enterpriseId, teamId, userId, isEnterpriseInstall);

        if (installation != null)
        {
            Installation? updatedInstallation = await PerformTokenRotation(slackClient, installation);
            if (updatedInstallation != null)
                installationStore.Save(updatedInstallation);
        }
    }

    private static async Task<Installation?> PerformTokenRotation(SlackClient slackClient, Installation installation, int minutesBeforeExpiration = 120)
    {
        Bot? rotatedBot = await PerformBotTokenRotation(slackClient, installation.ToBot(), minutesBeforeExpiration);
        Installation? rotatedInstallation = await PerformUserTokenRotation(slackClient, installation, minutesBeforeExpiration);

        if (rotatedBot != null && rotatedInstallation == null)
        {
            rotatedInstallation = new(installation)
            {
                BotToken = rotatedBot.BotToken,
                BotRefreshToken = rotatedBot.BotRefreshToken,
                BotTokenExpiresAt = rotatedBot.BotTokenExpiresAt,
            };
        }

        return rotatedInstallation;
    }

    private static async Task<Bot?> PerformBotTokenRotation(SlackClient slackClient, Bot bot, int minutesBeforeExpiration)
    {
        if (bot.BotTokenExpiresAt == null || bot.BotTokenExpiresAt > DateTimeOffset.UtcNow.ToUnixTimeSeconds() + minutesBeforeExpiration * 60)
            return null;

        var oAuthResponse = await slackClient.OAuthV2Success(new() { GrantType = "refresh_token", RefreshToken = bot.BotRefreshToken });

        if (oAuthResponse.TokenType != "bot")
            return null;

        Bot refreshedBot = new(bot)
        {
            BotToken = oAuthResponse.AccessToken,
            BotRefreshToken = oAuthResponse.RefreshToken,
            BotTokenExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + oAuthResponse.ExpiresIn
        };
        return refreshedBot;
    }

    private static async Task<Installation?> PerformUserTokenRotation(SlackClient slackClient, Installation installation, int minutesBeforeExpiration)
    {
        if (installation.UserTokenExpiresAt == null || installation.UserTokenExpiresAt > DateTimeOffset.UtcNow.ToUnixTimeSeconds() + minutesBeforeExpiration * 60)
            return null;

        var oAuthResponse = await slackClient.OAuthV2Success(new() { GrantType = "refresh_token", RefreshToken = installation.UserRefreshToken });

        if (oAuthResponse.TokenType != "user")
            return null;

        Installation refreshedInstallation = new(installation)
        {
            UserToken = oAuthResponse.AccessToken,
            UserRefreshToken = oAuthResponse.RefreshToken,
            UserTokenExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + oAuthResponse.ExpiresIn
        };

        return refreshedInstallation;
    }

    
}

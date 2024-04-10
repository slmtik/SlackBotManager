using SlackBotManager.API.Interfaces;
using SlackBotManager.API.Models.Stores;
using SlackBotManager.API.Services;
using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace SlackBotManager.API.MIddlewares;

public class SlackTokenRotator(RequestDelegate next)
{
    private readonly RequestDelegate _next = next;

    public async Task InvokeAsync(HttpContext context, IInstallationStore installationStore, SlackClient slackClient)
    {
        string rawBody = string.Empty;

        ReadResult readResult = await context.Request.BodyReader.ReadAsync();
        var buffer = readResult.Buffer;

        if (readResult.IsCompleted && buffer.Length > 0)
            rawBody = Encoding.UTF8.GetString(buffer.IsSingleSegment ? buffer.FirstSpan : buffer.ToArray().AsSpan());

        context.Request.BodyReader.AdvanceTo(buffer.Start, buffer.End);

        var (teamId, enterpriseId, isEnterpriseInstall, challenge) = ParseBody(rawBody, context.Request.Headers.ContentType.ToString());

        if (!string.IsNullOrEmpty(challenge))
        {
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            await context.Response.WriteAsync(challenge);
            return;
        }

        await RotateToken(installationStore, slackClient, teamId, enterpriseId, isEnterpriseInstall);

        Bot? bot = installationStore.FindBot(enterpriseId, teamId, isEnterpriseInstall);

        if (string.IsNullOrEmpty(bot?.BotToken))
        {
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            await context.Response.WriteAsync("Please install this app first!");
            return;
        }

        context.Items["bot-token"] = bot?.BotToken;

        await _next(context);
    }

    
    private static (string?, string?, bool?, string?) ParseBody(string body, string contentType)
    {
        string? teamId = null;
        string? enterpriseId = null;
        bool? isEnterpriseInstall = null;
        string? challenge = null;

        JsonNode? jsonBody = null;

        if (contentType == "application/json" || body.StartsWith('{'))
        {
            var json = JsonNode.Parse(body);
            if (json!["authorizations"] is JsonArray authorizations && authorizations.Count > 0)
                jsonBody = authorizations[0]!;
            else if (json!["type"]!.ToString() == "url_verification")
                challenge = json["challenge"]!.ToString();
        }
        else
        {
            var formBody = new Microsoft.AspNetCore.WebUtilities.FormReader(body).ReadForm();
            if (formBody.TryGetValue("payload", out var payload))
                jsonBody = JsonNode.Parse(payload!)!;
            else
                jsonBody = new JsonObject(formBody.Select(kvp => KeyValuePair.Create<string, JsonNode>(kvp.Key, kvp.Value.ToString()))!);
        }

        teamId = (jsonBody?["team_id"] ?? jsonBody?["team"]?["id"] ?? jsonBody?["team"] ?? jsonBody?["user"]?["team_id"])?.ToString();
        enterpriseId = (jsonBody?["enterprise_id"] ?? jsonBody?["enterprise"]?["id"] ?? jsonBody?["enterprise"])?.ToString();
        isEnterpriseInstall = Convert.ToBoolean(jsonBody?["is_enterprise_install"]?.ToString());

        return (teamId, enterpriseId, isEnterpriseInstall, challenge);
    }

    private static async Task RotateToken(IInstallationStore installationStore,
                                   SlackClient slackClient,
                                   string? teamId,
                                   string? enterpriseId,
                                   bool? isEnterpriseInstall)
    {
        var installation = installationStore.FindInstallation(enterpriseId, teamId, null, isEnterpriseInstall);

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

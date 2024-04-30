using SlackBotManager.API.Extensions;
using SlackBotManager.API.Interfaces.Stores;
using SlackBotManager.API.Models.Core;
using SlackBotManager.API.Models.Stores;
using SlackBotManager.API.Services;
using System.Net;
using System.Text.Json.Nodes;

namespace SlackBotManager.API.MIddlewares;

public class SlackTokenRotator(RequestDelegate next)
{
    private readonly RequestDelegate _next = next;

    public async Task InvokeAsync(HttpContext context, IInstallationStore installationStore, SlackClient client)
    {
        context.Request.EnableBuffering();
        string rawBody = await context.Request.BodyReader.GetStringFromPipe();
        context.Request.Body.Position = 0;

        if (IsChallengeRequest(rawBody, context.Request.Headers.ContentType.ToString(), out var challenge))
        {
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            await context.Response.WriteAsync(challenge);
            return;
        }

        var instanceData = ParseBody(rawBody, context.Request.Headers.ContentType.ToString());
        
        if (!await RotateToken(installationStore, client, instanceData))
        {
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            await context.Response.WriteAsync("Please install this app first!");
            return;
        }

        var installation = await installationStore.Find(instanceData.EnterpriseId, instanceData.TeamId, instanceData.IsEnterpriseInstall);

        context.Items[SlackClient.BotTokenHttpContextKey] = installation.BotToken;
        context.Items[InstanceData.HttpContextKey] = instanceData;

        await _next(context);
    }

    private static bool IsChallengeRequest(string body, string contentType, out string? challenge)
    {
        challenge = null;
        if (contentType == "application/json" || body.StartsWith('{'))
        {
            var json = JsonNode.Parse(body);
            if (json != null && json["type"]?.ToString() == "url_verification")
            {
                challenge = json["challenge"].ToString();
                return true;
            }
        }
        return false;
    }


    private static InstanceData ParseBody(string body, string contentType)
    {
        string? enterpriseId = null;
        string? teamId = null;
        bool isEnterpriseInstall;

        JsonNode? jsonBody = null;

        if (contentType == "application/json" || body.StartsWith('{'))
        {
            var json = JsonNode.Parse(body);
            if (json!["authorizations"] is JsonArray authorizations && authorizations.Count > 0)
                jsonBody = authorizations[0];
        }
        else
        {
            var formBody = new Microsoft.AspNetCore.WebUtilities.FormReader(body).ReadForm();
            if (formBody.TryGetValue("payload", out var payload))
                jsonBody = JsonNode.Parse(payload!)!;
            else
                jsonBody = new JsonObject(formBody.Select(kvp => KeyValuePair.Create<string, JsonNode>(kvp.Key, kvp.Value.ToString()))!);
        }

        enterpriseId = (jsonBody?["enterprise_id"] ?? jsonBody?["enterprise"]?["id"] ?? jsonBody?["enterprise"])?.ToString();
        teamId = (jsonBody?["team_id"] ?? jsonBody?["team"]?["id"] ?? jsonBody?["team"] ?? jsonBody?["user"]?["team_id"])?.ToString();
        isEnterpriseInstall = Convert.ToBoolean(jsonBody?["is_enterprise_install"]?.ToString());

        return new(enterpriseId, teamId, isEnterpriseInstall);
    }

    private static async Task<bool> RotateToken(IInstallationStore installationStore, SlackClient client, InstanceData instanceData)
    {
        var installation = await installationStore.Find(instanceData.EnterpriseId, instanceData.TeamId, instanceData.IsEnterpriseInstall);
        if (installation != null)
        {
            Installation? updatedInstallation = await PerformTokenRotation(client, installation);
            if (updatedInstallation != null)
                await installationStore.Save(updatedInstallation);
            return true;
        }
        return false;
    }

    private static async Task<Installation?> PerformTokenRotation(SlackClient client, Installation installation, int minutesBeforeExpiration = 120)
    {
        Installation? rotatedBotToken = await PerformBotTokenRotation(client, installation, minutesBeforeExpiration);
        Installation? rotatedUserToken = await PerformUserTokenRotation(client, installation, minutesBeforeExpiration);

        if (rotatedBotToken != null && rotatedUserToken == null)
            rotatedUserToken = installation with
            {
                BotToken = rotatedBotToken.BotToken,
                BotRefreshToken = rotatedBotToken.BotRefreshToken,
                BotTokenExpiresAt = rotatedBotToken.BotTokenExpiresAt,
            };

        return rotatedUserToken;
    }

    private static async Task<Installation?> PerformBotTokenRotation(SlackClient client, Installation installation, int minutesBeforeExpiration)
    {
        if (installation.BotTokenExpiresAt == null || installation.BotTokenExpiresAt > DateTimeOffset.UtcNow.ToUnixTimeSeconds() + minutesBeforeExpiration * 60)
            return null;

        var oAuthResult = await client.OAuthV2Success(new() { GrantType = "refresh_token", RefreshToken = installation.BotRefreshToken });
        if (!oAuthResult.IsSuccesful)
            return null; 

        var oAuthData = oAuthResult.Value!;
        if (oAuthData.TokenType != "bot")
            return null;

        Installation refreshedBot = installation with
        {
            BotToken = oAuthData.AccessToken,
            BotRefreshToken = oAuthData.RefreshToken,
            BotTokenExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + oAuthData.ExpiresIn
        };
        return refreshedBot;
    }

    private static async Task<Installation?> PerformUserTokenRotation(SlackClient client, Installation installation, int minutesBeforeExpiration)
    {
        if (installation.UserTokenExpiresAt == null || installation.UserTokenExpiresAt > DateTimeOffset.UtcNow.ToUnixTimeSeconds() + minutesBeforeExpiration * 60)
            return null;

        var oAuthResult = await client.OAuthV2Success(new() { GrantType = "refresh_token", RefreshToken = installation.UserRefreshToken });
        if (!oAuthResult.IsSuccesful)
            return null;

        var oAuthData = oAuthResult.Value!;
        if (oAuthData.TokenType != "user")
            return null;

        Installation refreshedInstallation = installation with
        {
            UserToken = oAuthData.AccessToken,
            UserRefreshToken = oAuthData.RefreshToken,
            UserTokenExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + oAuthData.ExpiresIn
        };
        return refreshedInstallation;
    }
}

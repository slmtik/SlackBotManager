using System.Net;
using System.Text.Json.Nodes;
using Persistence.Interfaces;
using Persistence.Models;
using Slack;
using API.Services;
using API.Extensions;

namespace API.MIddlewares;

public class InstallationTokenVerifier(RequestDelegate next)
{
    private readonly RequestDelegate _next = next;

    public async Task InvokeAsync(HttpContext context, IInstallationStore installationStore, SlackTokenRotator slackTokenRotator)
    {
        context.Request.EnableBuffering();
        string body = await context.Request.BodyReader.GetStringFromPipe();
        context.Request.Body.Position = 0;

        if (IsChallengeRequest(body, context.Request.Headers.ContentType.ToString(), out var challenge))
        {
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            await context.Response.WriteAsync(challenge);
            return;
        }

        var instanceData = ParseBody(body, context.Request.Headers.ContentType.ToString());

        if (!await slackTokenRotator.RotateToken(instanceData))
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
}

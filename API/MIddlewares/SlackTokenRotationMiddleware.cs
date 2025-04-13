using API.Services;
using Core;
using Persistence.Interfaces;
using System.Net;

namespace API.MIddlewares;

public class SlackTokenRotationMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next;

    public async Task InvokeAsync(HttpContext context, IInstallationStore installationStore, SlackTokenRotator slackTokenRotator, RequestContext requestContext)
    {
        string body = context.Items["RawBody"] as string
            ?? throw new InvalidOperationException($"Middleware {nameof(SlackTokenRotationMiddleware)} was called, but no body parsed");

        var instanceData = InstanceData.Parse(body);

        if (!await slackTokenRotator.RotateToken(instanceData))
        {
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            await context.Response.WriteAsync("Please install this app first!");
            return;
        }

        var installation = await installationStore.Find(instanceData.EnterpriseId, instanceData.TeamId, instanceData.IsEnterpriseInstall);

        requestContext.BotToken = installation.BotToken;
        requestContext.InstanceData = instanceData;

        await _next(context);
    }
}

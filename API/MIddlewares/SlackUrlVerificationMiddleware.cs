using System.Net;
using System.Text.Json.Nodes;

namespace API.MIddlewares;

public class SlackUrlVerificationMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        string body = context.Items["RawBody"] as string 
            ?? throw new InvalidOperationException($"Middleware {nameof(SlackUrlVerificationMiddleware)} was called, but no body parsed");

        if (IsChallengeRequest(body, context.Request.Headers.ContentType.ToString(), out var challenge))
        {
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            await context.Response.WriteAsync(challenge);
            return;
        }

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
}

using API.Extensions;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace API.MIddlewares;

public class SlackSignatureVerifier(RequestDelegate next, IConfiguration configuration)
{
    private readonly RequestDelegate _next = next;
    private readonly string _signingSecret = configuration["Slack:SigningSecret"];

    public async Task InvokeAsync(HttpContext context)
    {
        context.Request.EnableBuffering();
        var body = await context.Request.BodyReader.GetStringFromPipe();
        context.Request.Body.Position = 0;

        if (!ValidateSignature(body, context.Request.Headers))
        {
            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            return;
        }

        await _next(context);
    }

    private bool ValidateSignature(string body, IHeaderDictionary headers)
    {
        if (headers == null)
            return false;

        headers.TryGetValue("X-Slack-Request-Timestamp", out var timestampValue);
        headers.TryGetValue("X-Slack-Signature", out var signatureValue);

        var signature = signatureValue.ToString();

        if (string.IsNullOrEmpty(signature) || !long.TryParse(timestampValue, out var timestamp))
            return false;

        if (Math.Abs(DateTimeOffset.Now.ToUnixTimeSeconds() - timestamp) > 60 * 5)
            return false;

        return signature == GenerateSignature(timestamp, body);
    }

    private string? GenerateSignature(long timestamp, string stringToSign)
    {
        if (string.IsNullOrEmpty(stringToSign))
            stringToSign = string.Empty;

        if (string.IsNullOrEmpty(_signingSecret))
            throw new ArgumentException(nameof(_signingSecret));

        using var sha256 = new HMACSHA256(Encoding.UTF8.GetBytes(_signingSecret));
        var messageHash = sha256.ComputeHash(Encoding.UTF8.GetBytes($"v0:{timestamp}:{stringToSign}"));
        return $"v0={string.Concat(messageHash.Select(b => b.ToString("x2")))}";
    }
}

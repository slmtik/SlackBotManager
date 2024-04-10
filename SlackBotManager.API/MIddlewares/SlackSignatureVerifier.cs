using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace SlackBotManager.API.MIddlewares;

public class SlackSignatureVerifier(RequestDelegate next, IConfiguration configuration)
{
    private readonly RequestDelegate _next = next;
    private readonly string _signingSecret = configuration["Slack:SigningSecret"] ?? throw new ArgumentException("SigningSecret");

    public async Task InvokeAsync(HttpContext context)
    {
        string body = string.Empty;

        ReadResult readResult = await context.Request.BodyReader.ReadAsync();
        var buffer = readResult.Buffer;

        if (readResult.IsCompleted && buffer.Length > 0)
            body = Encoding.UTF8.GetString(buffer.IsSingleSegment ? buffer.FirstSpan : buffer.ToArray().AsSpan());

        context.Request.BodyReader.AdvanceTo(buffer.Start, buffer.End);

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

        headers.TryGetValue("X-Slack-Request-Timestamp", out var timestamp);
        headers.TryGetValue("X-Slack-Signature", out var signature);

        return ValidateSignature(body, timestamp.FirstOrDefault() ?? string.Empty, signature.FirstOrDefault() ?? string.Empty);
    }

    private bool ValidateSignature(string body, string timestamp, string signature)
    {
        if (string.IsNullOrEmpty(timestamp) || string.IsNullOrEmpty(signature))
            return false;

        if (Math.Abs(DateTimeOffset.Now.ToUnixTimeSeconds() - long.Parse(timestamp)) > 60 * 5)
            return false;

        return signature == GenerateSignature(timestamp, body);
    }

    private string? GenerateSignature(string timestamp, string stringToSign)
    {
        if (string.IsNullOrEmpty(timestamp))
            return null;

        if (string.IsNullOrEmpty(stringToSign))
            stringToSign = string.Empty;

        using var sha256 = new HMACSHA256(Encoding.UTF8.GetBytes(_signingSecret));
        var messageHash = sha256.ComputeHash(Encoding.UTF8.GetBytes($"v0:{timestamp}:{stringToSign}"));
        return $"v0={string.Concat(messageHash.Select(b => b.ToString("x2")))}";
    }
}

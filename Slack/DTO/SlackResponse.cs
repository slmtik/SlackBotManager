using Core.ApiClient;

namespace Slack.DTO;

public class SlackResponse: ResponseBase
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public string? Warning { get; set; }
    public ResponseMetadata? ResponseMetadata { get; set; }
}

public class ResponseMetadata
{
    public string[]? Messages { get; set; }
    public string[]? Warnings { get; set; }
}
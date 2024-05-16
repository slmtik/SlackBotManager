using System.Text.Json.Serialization;

namespace SlackBotManager.Slack;

public class ErrorSubmissionResponse : ISubmissionResponse
{
    [JsonPropertyName("errors")]
    public Dictionary<string, string> Errors { get; set; } = [];
}


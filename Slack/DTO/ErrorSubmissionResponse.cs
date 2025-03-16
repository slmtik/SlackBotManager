using Slack.Interfaces;
using System.Text.Json.Serialization;

namespace Slack.DTO;

public class ErrorSubmissionResponse : ISubmissionResponse
{
    [JsonPropertyName("errors")]
    public Dictionary<string, string> Errors { get; set; } = [];
}


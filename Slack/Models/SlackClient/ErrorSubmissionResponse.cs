using Slack.Interfaces;
using System.Text.Json.Serialization;

namespace Slack.Models.SlackClient;

public class ErrorSubmissionResponse : ISubmissionResponse
{
    [JsonPropertyName("errors")]
    public Dictionary<string, string> Errors { get; set; } = [];
}


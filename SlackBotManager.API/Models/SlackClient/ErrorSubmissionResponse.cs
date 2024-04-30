using SlackBotManager.API.Interfaces;
using System.Text.Json.Serialization;

namespace SlackBotManager.API.Models.SlackClient;

public class ErrorSubmissionResponse : ISubmissionResponse
{
    [JsonPropertyName("errors")]
    public Dictionary<string, string> Errors { get; set; } = [];
}


using SlackBotManager.API.Interfaces;
using SlackBotManager.API.Models.SlackClient;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SlackBotManager.API.Models.Payloads
{
    public class Message
    {
        [JsonPropertyName("ts")]
        public string TimestampId { get; set; }
        public IEnumerable<IBlock> Blocks { get; set; }
        public Metadata? Metadata { get; set; }
    }
}
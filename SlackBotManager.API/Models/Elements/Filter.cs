using System.Text.Json.Serialization;

namespace SlackBotManager.API.Models.Elements;

public class Filter
{
    private ConversationType[]? _conversationTypes;

    [JsonIgnore]
    public ConversationType[]? ConversationTypes 
    { 
        get => _conversationTypes;
        set
        {
            _conversationTypes = value;

            if (value != null)
            {
                Include = value.Select(ct => ct.Value).ToArray();
            }
        }
    }

    [JsonInclude]
    private string[]? Include { get; set; }
    public bool ExcludeExternalSharedChannels { get; set; }
    public bool ExcludeBotUsers { get; set; }
}

public class ConversationType
{
    private ConversationType(string value) { Value = value; }
    public string Value { get; }

    public static ConversationType DirectMessages => new("im");
    public static ConversationType MultipartyDirectMessages => new("mpim");
    public static ConversationType PrivateChannels => new("private");
    public static ConversationType PublicChannels => new("im");


}
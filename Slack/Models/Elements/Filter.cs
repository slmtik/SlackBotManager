namespace SlackBotManager.Slack.Elements;

public class Filter
{
    public static class ConversationType
    {
        public static readonly string DirectMessages = "im";
        public static readonly string MultipartyDirectMessages = "mpim";
        public static readonly string PrivateChannels = "private";
        public static readonly string PublicChannels = "im";
    }

    public string[]? Include { get; set; }
    public bool ExcludeExternalSharedChannels { get; set; }
    public bool ExcludeBotUsers { get; set; }
}


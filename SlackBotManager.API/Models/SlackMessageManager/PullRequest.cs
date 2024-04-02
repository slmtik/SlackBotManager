namespace SlackBotManager.API.Models.SlackMessageManager;

public class PullRequest(string channelId, string timestampID)
{
    public string ChannelId { get; set; } = channelId;
    public string TimestampID { get; set; } = timestampID;
    public IEnumerable<string> Branches { get; set; } = [];
    public int IssuesNumber { get; set; }
}

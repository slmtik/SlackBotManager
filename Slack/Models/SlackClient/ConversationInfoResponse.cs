using Slack.Models.SlackClient;

namespace Slack.Models.SlackClient;

public class ConversationInfoResponse : BaseResponse
{
    public Channel? Channel { get; set; }
}

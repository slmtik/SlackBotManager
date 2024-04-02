using Microsoft.AspNetCore.Mvc;

namespace SlackBotManager.API.Models.SlackClient;

public class CommandRequest
{
    public string Command { get; set; }

    [BindProperty(Name = "user_id")]
    public string UserId { get; set; }
    [BindProperty(Name = "trigger_id")]
    public string TriggerId { get; set; }
    [BindProperty(Name = "channel_id")]
    public string ChannelId { get; set; }
}

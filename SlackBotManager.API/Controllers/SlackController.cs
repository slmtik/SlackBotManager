using Microsoft.AspNetCore.Mvc;
using SlackBotManager.API.Models.SlackClient;
using SlackBotManager.API.Services;

namespace SlackBotManager.API.Controllers;

[Route("api/slack")]
[ApiController]
public class SlackController(SlackMessageManager slackMessageManager) : ControllerBase
{
    private readonly SlackMessageManager _slackMessageManager = slackMessageManager;

    [HttpPost]
    [Route("commands")]
    public async Task<IActionResult> HandleCommands([FromForm] CommandRequest slackCommand)
    {
        await _slackMessageManager.HandleCommand(slackCommand);
        return Ok();
    }

    [HttpPost]
    [Route("events")]
    public async Task<IActionResult> HandleEvents([FromForm] string payload)
    {
        await _slackMessageManager.HandlePayload(payload);
        return Ok();
    }
}

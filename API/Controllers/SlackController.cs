using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Nodes;
using Persistence.Interfaces;
using API.Services;
using Slack.Models.Events;
using Slack.Models.Commands;
using Slack.Interfaces;
using Slack;
using System.Text.Json;

namespace API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class SlackController(SlackManager slackManager,
                             SlackOAuthHelper slackOAuthHelper,
                             IOAuthStateStore oAuthStateStore) : ControllerBase
{
    [HttpPost]
    [Route("commands")]
    public async Task<ActionResult> HandleCommands([FromForm] Command slackCommand)
    {
        var requestResult = await slackManager.HandleCommand(slackCommand);

        if (!requestResult.IsSuccessful)
            return Ok(requestResult.Error);
        return Ok();
    }

    [HttpPost]
    [Route("interactions")]
    public async Task<ActionResult> HandleInteractions([FromForm] string payload)
    {
        var interactionPayload = JsonSerializer.Deserialize<IInteractionPayload>(payload, SlackClient.ApiJsonSerializerOptions);
        var requestResult = await slackManager.HandleInteractionPayload(interactionPayload);

        if (!requestResult.IsSuccessful)
        {
            try
            {
                return Ok(JsonNode.Parse(requestResult.Error!));
            }
            catch (JsonException)
            {
                return Ok(requestResult.Error);
            }
        }

        return Ok();
    }

    [HttpPost]
    [Route("events")]
    public async Task<ActionResult> HandleEvents(JsonNode payloadJSON)
    {
        payloadJSON["event"] = SlackManager.MakeTypePropertyFirstInPayload(payloadJSON["event"]);

        var payload = JsonSerializer.Deserialize<EventPayload>(payloadJSON, SlackClient.ApiJsonSerializerOptions);
        var requestResult = await slackManager.HandleEventPayload(payload);

        if (!requestResult.IsSuccessful)
        {
            try
            {
                return Ok(JsonNode.Parse(requestResult.Error!));
            }
            catch (JsonException)
            {
                return Ok(requestResult.Error);
            }
        }

        return Ok();
    }

    

    [HttpGet]
    [Route("install", Name="SlackOAuthInstall")]
    public ContentResult OAuthStart()
    {
        var state = oAuthStateStore.Issue();
        var url = slackOAuthHelper.GenerateOAuthURL(state);

        return base.Content($@"
            <html>
                <head>
                    <link rel=""icon"" href=""data:,"">
                </head>
                <body>
                    <a href=""{url}"">
                        <img alt=""Add to Slack"" height=""40"" width=""139"" src=""https://platform.slack-edge.com/img/add_to_slack.png"" 
                            srcset=""https://platform.slack-edge.com/img/add_to_slack.png 1x, https://platform.slack-edge.com/img/add_to_slack@2x.png 2x"" />
                    </a>
                </body>
            </html>", "text/html");
    }

    [HttpGet]
    [Route("oauth_redirect")]
    public async Task<ContentResult> OAuthCallback(string? code, string? state, string? error)
    {
        if (!string.IsNullOrEmpty(code) && !string.IsNullOrEmpty(state))
        {
            if (!oAuthStateStore.Consume(state))
                return base.Content(RenderFailurePage(Url.Link("OAuthInstall", null), "the state value is already expired"), "text/html");

            var result = await slackOAuthHelper.ProcessOauthCallback(code);

            if (result.IsSuccessful && result.Value is not null)
                return base.Content(RenderSuccessPage(result.Value.AppId, result.Value.TeamId, result.Value.IsEnterpriseInstall, result.Value.EnterpriseUrl),
                                    "text/html");

        }
        return base.Content(RenderFailurePage(Url.Link("SlackOAuthInstall", null), error), "text/html");
    }

    private static string RenderFailurePage(string? oAuthInstallUrl, string? error)
    {
        return $@"
            <html>
                <head>
                    <style>
                        body {{
                          padding: 10px 15px;
                          font-family: verdana;
                          text-align: center;
                        }}
                    </style>
                </head>
                <body>
                    <h2>Oops, Something Went Wrong!</h2>
                    <p>Please try again from <a href=""{oAuthInstallUrl}"">here</a> or contact the app owner (reason: {error})</p>
                </body>
            </html>";
    }

    private static string RenderSuccessPage(string? appId, string? teamId, bool? isEnterpriseInstall, string? enterpriseUrl)
    {
        string url;
        if (isEnterpriseInstall ?? false && !string.IsNullOrEmpty(enterpriseUrl) && !string.IsNullOrEmpty(appId))
            url = $"{enterpriseUrl}manage/organization/apps/profile/{appId}/workspaces/add";
        else if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(teamId))
            url = "slack://open";
        else
            url = $"slack://app?team={teamId}&id={appId}";

        string browserUrl = $"https://app.slack.com/client/{teamId}";

        return $@"
            <html>
                <head>
                    <meta http-equiv=""refresh"" content=""0; URL={url}"">
                    <style>
                        body {{
                          padding: 10px 15px;
                          font-family: verdana;
                          text-align: center;
                        }}
                    </style>
                </head>
                <body>
                    <h2>Thank you!</h2>
                    <p>Redirecting to the Slack App... click <a href=""{url}"">here</a>. If you use the browser version of Slack, click 
                        <a href=""{browserUrl}"" target=""_blank"">this link</a> instead.</p>
                </body>
            </html>";
    }
}
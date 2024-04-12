using Microsoft.AspNetCore.Mvc;
using SlackBotManager.API.Interfaces;
using SlackBotManager.API.Models.Events;
using SlackBotManager.API.Models.Stores;
using SlackBotManager.API.Services;
using SlackBotManager.API.Models.Commands;
using SlackBotManager.API.Models.Core;

namespace SlackBotManager.API.Controllers;

[Route("api/slack")]
[ApiController]
public class SlackController(SlackMessageManager slackMessageManager,
                             IOAuthStateStore oAuthStateStore,
                             AuthorizationUrlGenerator authorizationUrlGenerator,
                             SlackClient slackClient,
                             IInstallationStore installationStore,
                             IHostEnvironment hostEnvironment) : ControllerBase
{
    private const string _routeToOAuthStart = "/api/slack/install";

    private readonly SlackMessageManager _slackMessageManager = slackMessageManager;
    private readonly IOAuthStateStore _oAuthStateStore = oAuthStateStore;
    private readonly AuthorizationUrlGenerator _authorizationUrlGenerator = authorizationUrlGenerator;
    private readonly SlackClient _slackClient = slackClient;
    private readonly IInstallationStore _installationStore = installationStore;
    private readonly IHostEnvironment _hostEnvironment = hostEnvironment;

    [HttpPost]
    [Route("commands")]
    public async Task<ActionResult> HandleCommands([FromForm] Command slackCommand)
    {
        string commandPrefix = "/";
        if (_hostEnvironment.IsDevelopment() || _hostEnvironment.IsStaging())
            commandPrefix = $"/{(_hostEnvironment.IsDevelopment() ? "dev" : "stage")}_";

        slackCommand.CommandText = $"/{slackCommand.CommandText[commandPrefix.Length..]}";

        var requestResult = await _slackMessageManager.HandleCommand(slackCommand);

        if (!requestResult.IsSuccesful)
            return Ok(requestResult.Error);
        return Ok();
    }

    [HttpPost]
    [Route("interactions")]
    public async Task<ActionResult> HandleInteractions([FromForm] string payload)
    {
        await _slackMessageManager.HandleInteractionPayload(payload);
        return Ok();
    }

    [HttpPost]
    [Route("events")]
    public async Task<ActionResult> HandleEvents(EventPayload payload)
    {
        await _slackMessageManager.HandleEventPayload(payload);
        return Ok();
    }

    [HttpGet]
    [Route("install")]
    public ContentResult OAuthStart()
    {
        var state = _oAuthStateStore.Issue();
        var url = _authorizationUrlGenerator.Generate(state);

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
            if (!_oAuthStateStore.Consume(state))
                return base.Content(RenderFailurePage("the state value is already expired"), "text/html");

            var oAuthResult = await _slackClient.OAuthV2Success(new() { Code = code });
            var oAuthData = oAuthResult.Value;

            string? botId = null;
            string? enterpriseUrl = null;
            if (oAuthResult.IsSuccesful && !string.IsNullOrEmpty(oAuthData.AccessToken))
            {
                var authTestResult = await _slackClient.AuthTest(oAuthData.AccessToken);
                botId = authTestResult.Value.BotId;

                if (oAuthData.IsEnterpriseInstall)
                    enterpriseUrl = authTestResult.Value.Url;
            }

            var installation = new Installation()
            {
                AppId = oAuthData.AppId,
                EnterpriseId = oAuthData.Enterprise?.Id,
                EnterpriseName = oAuthData.Enterprise?.Name,
                EnterpriseUrl = enterpriseUrl,
                TeamId = oAuthData.Team?.Id,
                TeamName = oAuthData.Team?.Name,
                BotId = botId,
                BotToken = oAuthData.AccessToken,
                BotUserId = oAuthData.BotUserId,
                BotScopes = oAuthData.Scope,
                BotRefreshToken = oAuthData.RefreshToken,
                BotTokenExpiresIn = oAuthData.ExpiresIn,
                UserId = oAuthData.AuthedUser?.Id,
                UserToken = oAuthData.AuthedUser?.AccessToken,
                UserScopes = oAuthData.AuthedUser?.Scope,
                UserRefreshToken = oAuthData.AuthedUser?.RefreshToken,
                UserTokenExpiresIn = oAuthData.AuthedUser?.ExpiresIn,
                IsEnterpriseInstall = oAuthData.IsEnterpriseInstall,
                TokenType = oAuthData.TokenType
            };
            _installationStore.Save(installation);
            return base.Content(RenderSuccessPage(installation.AppId, installation.TeamId, installation.IsEnterpriseInstall, installation.EnterpriseUrl),
                                "text/html");

        }
        return base.Content(RenderFailurePage(error), "text/html");
    }

    private static string RenderFailurePage(string? error)
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
                    <p>Please try again from <a href=""{_routeToOAuthStart}"">here</a> or contact the app owner (reason: {error})</p>
                </body>
            </html>";
    }

    private static string RenderSuccessPage(string? appId, string? teamId, bool isEnterpriseInstall, string? enterpriseUrl)
    {
        string url;
        if (isEnterpriseInstall && !string.IsNullOrEmpty(enterpriseUrl) && !string.IsNullOrEmpty(appId))
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
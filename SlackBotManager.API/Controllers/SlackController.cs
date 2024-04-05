using Microsoft.AspNetCore.Mvc;
using SlackBotManager.API.Interfaces;
using SlackBotManager.API.Models.OAuth;
using SlackBotManager.API.Models.SlackClient;
using SlackBotManager.API.Services;

namespace SlackBotManager.API.Controllers;

[Route("api/slack")]
[ApiController]
public class SlackController(SlackMessageManager slackMessageManager,
                             IOAuthStateStore oAuthStateStore,
                             AuthorizationUrlGenerator authorizationUrlGenerator,
                             SlackClient slackClient,
                             IInstallationStore installationStore) : ControllerBase
{
    private const string _routeToOAuthStart = "/api/slack/install";

    private readonly SlackMessageManager _slackMessageManager = slackMessageManager;
    private readonly IOAuthStateStore _oAuthStateStore = oAuthStateStore;
    private readonly AuthorizationUrlGenerator _authorizationUrlGenerator = authorizationUrlGenerator;
    private readonly SlackClient _slackClient = slackClient;
    private readonly IInstallationStore _installationStore = installationStore;

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

            var oauthResponse = await _slackClient.OAuthV2Success(new() { Code = code });

            string? botId = null;
            string? enterpriseUrl = null;
            if (!string.IsNullOrEmpty(oauthResponse.AccessToken))
            {
                var authTestResponse = await _slackClient.AuthTest(oauthResponse.AccessToken);
                botId = authTestResponse.BotId;

                if (oauthResponse.IsEnterpriseInstall)
                    enterpriseUrl = authTestResponse.Url;
            }

            var installation = new Installation()
            {
                AppId = oauthResponse.AppId,
                EnterpriseId = oauthResponse.Enterprise?.Id,
                EnterpriseName = oauthResponse.Enterprise?.Name,
                EnterpriseUrl = enterpriseUrl,
                TeamId = oauthResponse.Team?.Id,
                TeamName = oauthResponse.Team?.Name,
                BotId = botId,
                BotToken = oauthResponse.AccessToken,
                BotUserId = oauthResponse.BotUserId,
                BotScopes = oauthResponse.Scope,
                BotRefreshToken = oauthResponse.RefreshToken,
                BotTokenExpiresIn = oauthResponse.ExpiresIn,
                UserId = oauthResponse.AuthedUser?.Id,
                UserToken = oauthResponse.AuthedUser?.AccessToken,
                UserScopes = oauthResponse.AuthedUser?.Scope,
                UserRefreshToken = oauthResponse.AuthedUser?.RefreshToken,
                UserTokenExpiresIn = oauthResponse.AuthedUser?.ExpiresIn,
                IsEnterpriseInstall = oauthResponse.IsEnterpriseInstall,
                TokenType = oauthResponse.TokenType
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
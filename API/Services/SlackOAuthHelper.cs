using Core.ApiClient;
using Persistence.Interfaces;
using Persistence.Models;
using Slack;

namespace API.Services
{
    public class SlackOAuthHelper(SlackClient slackClient, IInstallationStore installationStore, IConfiguration configuration)
    {
        public string GenerateOAuthURL(string state)
        {
            var clientId = configuration["Slack:ClientId"];
            var scopes = string.Join(",", configuration.GetSection("Slack:Scopes").Get<string[]?>() ?? []);
            var userScopes = string.Join(",", configuration.GetSection("Slack:UserScopes").Get<string[]?>() ?? []);

            var queryParams = string.Join("&", $"state={state}", $"client_id={clientId}", $"scope={scopes}", $"user_scope={userScopes}");

            return $"https://slack.com/oauth/v2/authorize?{queryParams}";
        }

        public async Task<IRequestResult<Installation>> ProcessOauthCallback(string code)
        {
            var oAuthResult = await slackClient.OAuthV2Success(new() { Code = code });
            var oAuthData = oAuthResult.Value;

            string? botId = null;
            string? enterpriseUrl = null;
            if (oAuthResult.IsSuccessful)
            {
                var authTestResult = await slackClient.AuthTest(oAuthResult.Value.AccessToken);
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
                IsEnterpriseInstall = oAuthData.IsEnterpriseInstall,
                TokenType = oAuthData.TokenType
            };
            await installationStore.Save(installation);

            return RequestResult<Installation>.Success(installation);
        }
    }
}

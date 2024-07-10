using SlackBotManager.Slack;
using SlackBotManager.Persistence;
using SlackBotManager.Persistence.Models;

namespace SlackBotManager.API.Services;

public class SlackTokenRotator
{
    private readonly IInstallationStore _installationStore;
    private readonly SlackClient _client;

    public SlackTokenRotator(IInstallationStore installationStore, SlackClient client)
    {
        _installationStore = installationStore;
        _client = client;
    }

    public async Task<bool> RotateToken(InstanceData instanceData)
    {
        var installation = await _installationStore.Find(instanceData.EnterpriseId, instanceData.TeamId, instanceData.IsEnterpriseInstall);
        if (installation != null)
        {
            Installation? updatedInstallation = await PerformTokenRotation(installation);
            if (updatedInstallation != null)
                await _installationStore.Save(updatedInstallation);
            return true;
        }
        return false;
    }

    private async Task<Installation?> PerformTokenRotation(Installation installation, int minutesBeforeExpiration = 120)
    {
        Installation? rotatedBotToken = await PerformBotTokenRotation(installation, minutesBeforeExpiration);
        Installation? rotatedUserToken = await PerformUserTokenRotation(installation, minutesBeforeExpiration);

        if (rotatedBotToken != null && rotatedUserToken == null)
            rotatedUserToken = installation with
            {
                BotToken = rotatedBotToken.BotToken,
                BotRefreshToken = rotatedBotToken.BotRefreshToken,
                BotTokenExpiresAt = rotatedBotToken.BotTokenExpiresAt,
            };

        return rotatedUserToken;
    }

    private async Task<Installation?> PerformBotTokenRotation(Installation installation, int minutesBeforeExpiration)
    {
        if (installation.BotTokenExpiresAt == null || installation.BotTokenExpiresAt > DateTimeOffset.UtcNow.ToUnixTimeSeconds() + minutesBeforeExpiration * 60)
            return null;

        var oAuthResult = await _client.OAuthV2Success(new() { GrantType = "refresh_token", RefreshToken = installation.BotRefreshToken });
        if (!oAuthResult.IsSuccesful)
            return null; 

        var oAuthData = oAuthResult.Value!;
        if (oAuthData.TokenType != "bot")
            return null;

        Installation refreshedBot = installation with
        {
            BotToken = oAuthData.AccessToken,
            BotRefreshToken = oAuthData.RefreshToken,
            BotTokenExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + oAuthData.ExpiresIn
        };
        return refreshedBot;
    }

    private async Task<Installation?> PerformUserTokenRotation(Installation installation, int minutesBeforeExpiration)
    {
        if (installation.UserTokenExpiresAt == null || installation.UserTokenExpiresAt > DateTimeOffset.UtcNow.ToUnixTimeSeconds() + minutesBeforeExpiration * 60)
            return null;

        var oAuthResult = await _client.OAuthV2Success(new() { GrantType = "refresh_token", RefreshToken = installation.UserRefreshToken });
        if (!oAuthResult.IsSuccesful)
            return null;

        var oAuthData = oAuthResult.Value!;
        if (oAuthData.TokenType != "user")
            return null;

        Installation refreshedInstallation = installation with
        {
            UserToken = oAuthData.AccessToken,
            UserRefreshToken = oAuthData.RefreshToken,
            UserTokenExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + oAuthData.ExpiresIn
        };
        return refreshedInstallation;
    }
}

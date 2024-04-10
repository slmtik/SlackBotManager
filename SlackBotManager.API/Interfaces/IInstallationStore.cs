using SlackBotManager.API.Models.Stores;

namespace SlackBotManager.API.Interfaces;

public interface IInstallationStore
{
    public Installation? FindInstallation(string? enterpriseId, string? teamId, string? userId, bool? isEnterpriseInstall);
    public void Save(Installation installation);
    public Bot? FindBot(string? enterpriseId, string? teamId, bool? isEnterpriseInstall);
}
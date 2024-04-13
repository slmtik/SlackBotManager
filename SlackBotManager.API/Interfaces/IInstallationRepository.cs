using SlackBotManager.API.Models.Repositories;

namespace SlackBotManager.API.Interfaces;

public interface IInstallationRepository : IRepository<Installation>
{
    //public Installation? FindInstallation(string? enterpriseId, string? teamId, string? userId, bool? isEnterpriseInstall);
    //public void Save(Installation instance);
    public Bot? FindBot(string? enterpriseId, string? teamId, bool? isEnterpriseInstall);
}
using SlackBotManager.API.Models.Repositories;

namespace SlackBotManager.API.Interfaces;

public interface IInstallationRepository : IRepository<Installation>
{
    public Task<Bot?> FindBot(string? enterpriseId, string? teamId, bool? isEnterpriseInstall);
}
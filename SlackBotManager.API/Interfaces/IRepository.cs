namespace SlackBotManager.API.Interfaces
{
    public interface IRepository<T>
    {
        public Task<T?> Find(string? enterpriseId, string? teamId, string? userId, bool? isEnterpriseInstall);
        public Task Save(T instance);
    }
}

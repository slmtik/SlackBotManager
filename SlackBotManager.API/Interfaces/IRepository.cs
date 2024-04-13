namespace SlackBotManager.API.Interfaces
{
    public interface IRepository<T>
    {
        public T? Find(string? enterpriseId, string? teamId, string? userId, bool? isEnterpriseInstall);
        public void Save(T instance);
    }
}

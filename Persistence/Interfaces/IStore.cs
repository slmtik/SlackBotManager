namespace SlackBotManager.Persistence
{
    public interface IStore<T> where T : StoreItemBase
    {
        public Task<T?> Find(string? enterpriseId, string? teamId, bool? isEnterpriseInstall);
        public Task<T?> Find();
        public Task Save(T instance);
    }
}

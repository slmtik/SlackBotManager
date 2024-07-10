﻿using SlackBotManager.Persistence.Models;

namespace SlackBotManager.Persistence
{
    public interface IStore<T> where T : StoreItemBase
    {
        public Task<T?> Find(string? enterpriseId, string? teamId, bool? isEnterpriseInstall);
        public Task<T?> Find(InstanceData instanceData) => Find(instanceData.EnterpriseId, instanceData.TeamId, instanceData.IsEnterpriseInstall);
        public Task<T?> Find();
        public Task Save(T instance);
    }
}

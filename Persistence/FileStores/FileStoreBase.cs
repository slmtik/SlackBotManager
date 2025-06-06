﻿using Core;
using Microsoft.Extensions.Configuration;
using Persistence.Interfaces;
using Persistence.Models;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace Persistence.FileStores;

public abstract class FileStoreBase<T> : IStore<T> where T : StoreItemBase
{
    protected abstract string ConfigurationSection { get; }
    protected abstract string ConfigurationFolder { get; }
    protected abstract string ConfigurationFile { get; }
    protected const string _placeholder = "none";
    protected readonly string _directory;

    private readonly RequestContext _requestContext;

    protected FileStoreBase(IConfiguration configuration, RequestContext requestContext)
    {
        _directory = configuration[ConfigurationSection] ??
            Path.Combine(
                Environment.GetFolderPath(
                    string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME"))
                        ? Environment.SpecialFolder.CommonApplicationData
                        : Environment.SpecialFolder.UserProfile
                ),
                "SlackBotManager", ConfigurationFolder
            );

        _requestContext = requestContext;
    }

    public virtual async Task<T?> Find(string? enterpriseId, string? teamId, bool? isEnterpriseInstall)
    {
        enterpriseId ??= _placeholder;
        teamId = teamId is null || (isEnterpriseInstall ?? false) ? _placeholder : teamId;

        string filePath = Path.Combine(_directory, $"{enterpriseId}-{teamId}", $"{ConfigurationFile}-latest");

        T? instance = null;
        if (!File.Exists(filePath)) return instance;

        using var reader = new StreamReader(filePath);
        var content = await reader.ReadToEndAsync();
        if (string.IsNullOrEmpty(content)) return instance;

        return JsonSerializer.Deserialize<T>(content);
    }

    private InstanceData GetInstanceDataFromContext()
    {
        return _requestContext.InstanceData ?? throw new InvalidOperationException($"There is no Instance Data in the {nameof(RequestContext)}");
    }

    public virtual Task<T?> Find()
    {
        var instanceData = GetInstanceDataFromContext();
        return Find(instanceData.EnterpriseId, instanceData.TeamId, instanceData.IsEnterpriseInstall);
    }

    public virtual Task Save(T instance)
    {
        if (string.IsNullOrEmpty(instance.EnterpriseId) && string.IsNullOrEmpty(instance.TeamId))
        {
            var instanceData = GetInstanceDataFromContext();
            instance.EnterpriseId = instanceData.EnterpriseId;
            instance.TeamId = instanceData.TeamId;
        }

        var enterpriseId = instance.EnterpriseId ?? _placeholder;
        var teamId = instance.TeamId ?? _placeholder;

        var filePath = Path.Combine(_directory, $"{enterpriseId}-{teamId}");
        Directory.CreateDirectory(filePath);

        var instanceFilePath = Path.Combine(filePath, $"{ConfigurationFile}-latest");
        using var writer = new StreamWriter(instanceFilePath);
        var content = JsonSerializer.Serialize(instance);
        return writer.WriteAsync(content);
    }

    public async Task<Collection<T>> FindAll()
    {
        var allData = new Collection<T>();
        var directoryInfo = new DirectoryInfo(_directory);
        if (directoryInfo.Exists)
        {
            foreach (var directory in directoryInfo.GetDirectories().Select(d => d.Name.Split("-")))
            {
                var (enterpriseId, teamId) = (directory[0], directory[1]);
                T? instanceData = await Find(enterpriseId, teamId, teamId.Equals(_placeholder));
                if (instanceData != null)
                {
                    allData.Add(instanceData);
                }
            }
        }

        return allData;
    }
}

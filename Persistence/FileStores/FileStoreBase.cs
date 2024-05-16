using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using SlackBotManager.Persistence.Models;
using System.Text.Json;

namespace SlackBotManager.Persistence.FileStores;

public abstract class FileStoreBase<T> : IStore<T> where T : StoreItemBase
{
    abstract protected string ConfigurationSection { get; }
    abstract protected string ConfigurationFolder { get; }
    abstract protected string ConfigurationFile { get; }
    protected const string _placeholder = "none";
    protected readonly string _directory;
    
    private readonly IHttpContextAccessor _httpContextAccessor;

    protected FileStoreBase(IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
    {
        _directory = configuration[ConfigurationSection] ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "SlackBotManager", ConfigurationFolder);
        _httpContextAccessor = httpContextAccessor;
    }

    public virtual async Task<T?> Find(string? enterpriseId, string? teamId, bool? isEnterpriseInstall)
    {
        enterpriseId ??= _placeholder;
        teamId = teamId is null || (isEnterpriseInstall ?? false) ? _placeholder : teamId;

        string filePath = Path.Combine(_directory, $"{enterpriseId}-{teamId}", $"{ConfigurationFile}-latest");

        T? instance = null;
        if (!File.Exists(filePath))
            return instance;

        using var reader = new StreamReader(filePath);
        var content = await reader.ReadToEndAsync();
        return JsonSerializer.Deserialize<T>(content);
    }

    private InstanceData GetInstanceDataFromContext()
    {
        if (_httpContextAccessor.HttpContext is HttpContext httpContext)
        {
            if (httpContext.Items[InstanceData.HttpContextKey] != null)
                return (InstanceData)httpContext.Items[InstanceData.HttpContextKey]!;
            throw new InvalidOperationException($"There is no Instance Data in the {nameof(HttpContext)}");
        }
        throw new InvalidOperationException($"There is no {nameof(HttpContext)}, please use in the correct place");
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
}

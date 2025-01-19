using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Persistence.Interfaces;
using Persistence.Models;

namespace Persistence.FileStores;

public class FileInstallationStore(IConfiguration configuration, IHttpContextAccessor httpContextAccessor) :
    FileStoreBase<Installation>(configuration, httpContextAccessor), IInstallationStore
{
    protected override string ConfigurationSection => "Slack:installationStoreLocation";
    protected override string ConfigurationFolder => ".installation";
    protected override string ConfigurationFile => "installer";
}

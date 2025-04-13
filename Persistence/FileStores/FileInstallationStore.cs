using Core;
using Microsoft.Extensions.Configuration;
using Persistence.Interfaces;
using Persistence.Models;

namespace Persistence.FileStores;

public class FileInstallationStore(IConfiguration configuration, RequestContext requestContext) :
    FileStoreBase<Installation>(configuration, requestContext), IInstallationStore
{
    protected override string ConfigurationSection => "Slack:installationStoreLocation";
    protected override string ConfigurationFolder => ".installation";
    protected override string ConfigurationFile => "installer";
}

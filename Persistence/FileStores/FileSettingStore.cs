﻿using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using SlackBotManager.Persistence.Models;

namespace SlackBotManager.Persistence.FileStores;

public class FileSettingStore(IConfiguration configuration, IHttpContextAccessor httpContextAccessor) :
    FileStoreBase<Setting>(configuration, httpContextAccessor), ISettingStore
{
    protected override string ConfigurationSection => "Slack:SettingStoreLocation";
    protected override string ConfigurationFolder => ".setting";
    protected override string ConfigurationFile => "setting";
}

﻿using Microsoft.Extensions.Configuration;
using Persistence.Interfaces;

namespace Persistence.FileStores;

public class FileOAuthStateStore(IConfiguration configuration) : IOAuthStateStore
{
    private readonly string _directory = configuration["Slack:OAuthStateStoreLocation"] ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "SlackBotManager", ".oauth-state");

    private readonly int _expirationSeconds = 300;

    public bool Consume(string state)
    {
        var filePath = Path.Combine(_directory, state);
        if (!File.Exists(filePath))
            return false;

        bool isStillValid = false;
        using (var reader = new StreamReader(filePath))
        {
            if (long.TryParse(reader.ReadToEnd(), out var created))
            {
                var expidationTime = created + _expirationSeconds;
                isStillValid = DateTimeOffset.Now.ToUnixTimeSeconds() < expidationTime;
            }
        }

        File.Delete(filePath);

        return isStillValid;
    }

    public string Issue()
    {
        var state = Guid.NewGuid().ToString();
        Directory.CreateDirectory(_directory);

        using var writer = new StreamWriter(Path.Combine(_directory, state));
        var content = DateTimeOffset.Now.ToUnixTimeSeconds().ToString();
        writer.Write(content);

        return state;
    }
}

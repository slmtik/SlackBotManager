using System.ComponentModel;

namespace API.VersionStrategists;

public class ManualVersionStrategist : VersionStrategistBase
{
    override public string Name => "manual";
    override public string Description => "Manual Version Assignment";

    override public Task<string> GetVersion(Dictionary<string, string>? values = null) 
    {
        string version = "";
        values?.TryGetValue(nameof(Version).ToLower(), out version!);
        return Task.FromResult(version);    
    }

    [Description("Specify the version number.")]
    public string? Version { get; set; }
}
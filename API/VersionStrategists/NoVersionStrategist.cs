namespace API.VersionStrategists;

public class NoVersionStrategist : VersionStrategistBase
{
    public override string Name => "none";
    public override string Description => "No Version Strategy";

    public override Task<string> GetVersion(Dictionary<string, string>? values = null) => Task.FromResult("");
}

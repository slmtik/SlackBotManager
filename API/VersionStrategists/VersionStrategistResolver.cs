using API.Interfaces;

namespace API.VersionStrategists;

public class VersionStrategistResolver(IEnumerable<IVersionStrategist> versionStrategists) : IVersionStrategistResolver
{
    private readonly Dictionary<string, IVersionStrategist> _versionStrategists = 
        versionStrategists.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

    public IVersionStrategist GetStrategist(string strategyName)
    {
        if (_versionStrategists.TryGetValue(strategyName, out var versionStrategist))
        {
            return versionStrategist;
        }
        throw new ArgumentException($"No strategist found for {strategyName}");
    }

    public IEnumerable<(string, string)> GetAllStrategists()
    {
        return _versionStrategists.Values.Select(s => (s.Name, s.Description));
    }
}

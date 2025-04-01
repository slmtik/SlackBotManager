namespace Persistence.Models;

public class VersionStrategy
{
    public required string Name { get; set; }
    public Dictionary<string, string> Values { get; set; } = [];
}

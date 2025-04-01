namespace API.Interfaces;

public interface IVersionStrategistResolver
{
    IVersionStrategist GetStrategist(string strategyName);
    IEnumerable<(string Name, string Description)> GetAllStrategists();
}
namespace Persistence.Interfaces;

public interface IOAuthStateStore
{
    public string Issue();
    public bool Consume(string state);
}
namespace SlackBotManager.API.Interfaces.Stores;

public interface IOAuthStateStore
{
    public string Issue();
    public bool Consume(string state);
}
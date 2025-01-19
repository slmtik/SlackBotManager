namespace Slack.Interfaces;

public interface IRequestResult
{
    string? Error { get; set; }
    bool IsSuccesful { get; set; }
}

public interface IRequestResult<T> : IRequestResult
{
    public T? Value { get; set; }
}

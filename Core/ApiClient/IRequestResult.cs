namespace Core.ApiClient;

public interface IRequestResult
{
    string? Error { get; }
    bool IsSuccessful { get; }
}

public interface IRequestResult<T> : IRequestResult
{
    public T? Value { get; }
}

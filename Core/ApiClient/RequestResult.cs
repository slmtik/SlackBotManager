namespace Core.ApiClient;

public record RequestResult(bool IsSuccessful, string? Error) : IRequestResult
{
    public static RequestResult Success() => new(true, null);
    public static RequestResult Failure(string errorMessage) => new(false, errorMessage);
}

public record RequestResult<T>(bool IsSuccessful, T? Value, string? Error) : IRequestResult<T>
{
    public static RequestResult<T> Success(T Value) => new(true, Value, null);
    public static RequestResult<T> Failure(string errorMessage) => new(false, default, errorMessage);
}

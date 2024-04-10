﻿using SlackBotManager.API.Interfaces;

namespace SlackBotManager.API.Models.Core;

public class RequestResult : IRequestResult
{
    public bool IsSuccesful { get; set; }
    public string? Error { get; set; }

    public static RequestResult Success() => new() { IsSuccesful = true };
    public static RequestResult Failue(string errorMessage) => new() { IsSuccesful = false, Error = errorMessage };
}

public class RequestResult<T> : IRequestResult<T>
{
    public bool IsSuccesful { get; set; }
    public string? Error { get; set; }
    public T? Value { get; set; }

    public static RequestResult<T> Success(T value) => new() { IsSuccesful = true, Value = value };
    public static RequestResult<T> Failue(string errorMessage) => new() { IsSuccesful = false, Error = errorMessage };
}

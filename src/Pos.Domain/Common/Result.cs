namespace Pos.Domain.Common;

/// <summary>The outcome of an operation that can fail in an expected way. Reading the wrong side is a bug and throws.</summary>
public sealed class Result
{
    private static readonly Result Success = new(null);
    private readonly Error? _error;

    private Result(Error? error) => _error = error;

    public bool IsSuccess => _error is null;

    public Error Error => _error ?? throw new InvalidOperationException("A successful result has no error.");

    public static Result Ok() => Success;

    public static Result<T> Ok<T>(T data) => new(data, null);

    public static Result Fail(Error error) => new(error);

    public static Result<T> Fail<T>(Error error) => new(default, error);
}

/// <inheritdoc cref="Result"/>
public sealed class Result<T>
{
    private readonly T? _data;
    private readonly Error? _error;

    internal Result(T? data, Error? error)
    {
        _data = data;
        _error = error;
    }

    public bool IsSuccess => _error is null;

    public T Data => IsSuccess ? _data! : throw new InvalidOperationException("A failed result has no data.");

    public Error Error => _error ?? throw new InvalidOperationException("A successful result has no error.");
}

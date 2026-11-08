using System.Diagnostics.CodeAnalysis;

namespace Pos.Domain.Common;

/// <summary>
/// The outcome of an operation that can fail in an expected way. The unused side is null: <see cref="Error"/> on a
/// success, <see cref="Result{T}.Data"/> on a failure. A failure always has an <see cref="Error"/>; a success may have
/// null <see cref="Result{T}.Data"/>.
/// </summary>
public sealed class Result
{
    private static readonly Result Success = new(null);

    private Result(Error? error) => Error = error;

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public Error? Error { get; }

    public static Result Ok() => Success;

    public static Result<T> Ok<T>(T data) => new(data, null);

    public static Result Fail(Error error) => new(error);

    public static Result<T> Fail<T>(Error error) => new(default, error);
}

/// <inheritdoc cref="Result"/>
// T? does not make a value type nullable: a failed Result<bool> has Data false; use bool? for null.
public sealed class Result<T>
{
    internal Result(T? data, Error? error)
    {
        Data = data;
        Error = error;
    }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public T? Data { get; }

    public Error? Error { get; }
}

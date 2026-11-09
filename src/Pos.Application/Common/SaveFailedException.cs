using Pos.Domain.Common;

namespace Pos.Application.Common;

/// <summary>
/// A repository save that failed for an expected reason. The handler catches it, logs it and answers a failed
/// <see cref="Result"/> with <see cref="Code"/>; any other exception is a bug and ends as a 500.
/// </summary>
public abstract class SaveFailedException(ErrorCode code, string message, Exception innerException)
    : Exception(message, innerException)
{
    public ErrorCode Code { get; } = code;

    public Error ToError() => new(Code, Message);
}

/// <summary>Another request changed the same data first (concurrency token or deadlock).</summary>
public sealed class ConcurrentChangeException(string message, Exception innerException)
    : SaveFailedException(ErrorCode.ConcurrentChange, message, innerException);

/// <summary>The change breaks a unique or foreign-key constraint.</summary>
public sealed class DataConflictException(string message, Exception innerException)
    : SaveFailedException(ErrorCode.DataConflict, message, innerException);

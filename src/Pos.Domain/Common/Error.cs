namespace Pos.Domain.Common;

public record Error(ErrorCode Code, string Message);

/// <summary>A request of the wrong shape: <see cref="Fields"/> holds the messages per field.</summary>
public sealed record ValidationError(IReadOnlyDictionary<string, string[]> Fields)
    : Error(ErrorCode.ValidationFailed, "One or more validation errors occurred.");

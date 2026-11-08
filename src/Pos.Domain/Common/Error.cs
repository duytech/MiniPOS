namespace Pos.Domain.Common;

public record Error(ErrorCode Code, string Message)
{
    /// <summary>A request of the wrong shape: every failure message, joined into one.</summary>
    public static Error Validation(IEnumerable<string> messages) =>
        new(ErrorCode.ValidationFailed, string.Join(" ", messages));
}

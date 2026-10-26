namespace Pos.Application.Common;

/// <summary>The change could not be saved because of a concurrent change or a database constraint (maps to 409).</summary>
public class ConflictException(string message, Exception? innerException = null) : Exception(message, innerException);

namespace Pos.Application.Common;

/// <summary>Invalid input from the caller (mapped to 400 Bad Request).</summary>
public class ValidationException(string message) : Exception(message);

namespace Pos.Domain.Common;

/// <summary>Raised when a business rule of the domain model is violated.</summary>
public class DomainException(string message) : Exception(message);

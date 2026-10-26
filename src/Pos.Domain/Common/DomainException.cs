namespace Pos.Domain.Common;

/// <summary>A business rule of the domain was broken (maps to 422).</summary>
public class DomainException(string message) : Exception(message);

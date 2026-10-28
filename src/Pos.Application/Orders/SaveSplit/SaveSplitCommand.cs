namespace Pos.Application.Orders.SaveSplit;

/// <summary>
/// Saves the split by item of an open order, replacing any saved one: <paramref name="Parts"/> lists the line ids of
/// each part, in part order. Null values are rejected by the validator.
/// </summary>
public sealed record SaveSplitCommand(Guid OrderId, IReadOnlyList<IReadOnlyList<Guid>?>? Parts);

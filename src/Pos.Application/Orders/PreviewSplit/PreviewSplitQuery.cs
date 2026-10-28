namespace Pos.Application.Orders.PreviewSplit;

/// <summary>
/// Prices a split by item of an open order without saving it: <paramref name="Parts"/> lists the line ids of each
/// part, in part order. Null values are rejected by the validator.
/// </summary>
public sealed record PreviewSplitQuery(Guid OrderId, IReadOnlyList<IReadOnlyList<Guid>?>? Parts);

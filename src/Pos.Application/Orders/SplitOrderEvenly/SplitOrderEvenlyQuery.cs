namespace Pos.Application.Orders.SplitOrderEvenly;

/// <summary>Splits the order's bill evenly between <paramref name="Parts"/> people. Nothing is stored.</summary>
public sealed record SplitOrderEvenlyQuery(Guid OrderId, int Parts);

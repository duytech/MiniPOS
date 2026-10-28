namespace Pos.Application.Orders.GetSplit;

/// <summary>The order's saved split by item, priced with the current order; any status.</summary>
public sealed record GetSplitQuery(Guid OrderId);

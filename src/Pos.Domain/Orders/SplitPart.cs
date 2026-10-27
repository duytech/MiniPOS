using Pos.Domain.Common;

namespace Pos.Domain.Orders;

/// <summary>
/// One part of a split bill: its subtotal plus its share of the order's discount, surcharge, tax and tip.
/// A split by item lists the part's whole lines; an even split has no lines.
/// </summary>
public sealed record SplitPart(
    IReadOnlyList<Guid> LineIds,
    Money Subtotal,
    Money Discount,
    Money Surcharge,
    Money Tax,
    Money Tip,
    Money Total);

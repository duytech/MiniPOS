using Pos.Domain.Common;

namespace Pos.Domain.Orders;

/// <summary>One part of a split bill: whole order lines plus its share of the order's discount, surcharge, tax and tip.</summary>
public sealed record SplitPart(
    IReadOnlyList<Guid> LineIds,
    Money Subtotal,
    Money Discount,
    Money Surcharge,
    Money Tax,
    Money Tip,
    Money Total);

using Pos.Domain.Common;
using Pos.Domain.Orders;

namespace Pos.Application.Orders;

public sealed record SplitPartDto(
    IReadOnlyList<Guid> LineIds,
    Money Subtotal,
    Money Discount,
    Money Surcharge,
    Money Tax,
    Money Tip,
    Money Total)
{
    public static SplitPartDto From(SplitPart part) =>
        new(part.LineIds, part.Subtotal, part.Discount, part.Surcharge, part.Tax, part.Tip, part.Total);
}

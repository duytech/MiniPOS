using Pos.Domain.Orders;

namespace Pos.Application.Orders;

/// <summary>A split by item: its parts in saved or requested order, each priced by the order.</summary>
public sealed record OrderSplitDto(IReadOnlyList<SplitPartDto> Parts)
{
    public static OrderSplitDto From(IReadOnlyList<SplitPart> parts) => new(parts.Select(SplitPartDto.From).ToList());
}

using Pos.Domain.Common;
using Pos.Domain.Orders;

namespace Pos.Application.Orders;

/// <summary>An order in the orders list. The total comes from the order's pricing method, same as the detail.</summary>
public sealed record OrderSummaryDto(
    Guid Id,
    int Number,
    OrderStatus Status,
    DateTimeOffset CreatedAt,
    IReadOnlyList<string> ItemNames,
    Money Total)
{
    public static OrderSummaryDto From(Order order) => new(
        order.Id,
        order.Number,
        order.Status,
        order.CreatedAt,
        order.Lines.Select(l => l.Name).Distinct().ToList(),
        order.CalculateTotals().Total);
}

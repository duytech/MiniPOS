using Pos.Domain.Common;
using Pos.Domain.Orders;

namespace Pos.Application.Orders;

public sealed record OrderDto(Guid Id, IReadOnlyList<OrderLineDto> Lines, Money Subtotal, Money Total)
{
    public static OrderDto From(Order order)
    {
        var totals = order.CalculateTotals();
        return new OrderDto(order.Id, order.Lines.Select(OrderLineDto.From).ToList(), totals.Subtotal, totals.Total);
    }
}

public sealed record OrderLineDto(
    Guid Id,
    Guid MenuItemId,
    string Name,
    Money UnitPrice,
    int Quantity,
    string? Notes,
    Money LineTotal)
{
    public static OrderLineDto From(OrderLine line) =>
        new(line.Id, line.MenuItemId, line.Name, line.UnitPrice, line.Quantity, line.Notes, line.LineTotal);
}

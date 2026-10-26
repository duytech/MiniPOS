using Pos.Domain.Common;
using Pos.Domain.Orders;

namespace Pos.Application.Orders;

public sealed record OrderDto(
    Guid Id,
    int Number,
    OrderStatus Status,
    DateTimeOffset CreatedAt,
    PaymentMethod PaymentMethod,
    IReadOnlyList<OrderLineDto> Lines,
    Money Subtotal,
    Money Discount,
    Money Surcharge,
    Money Tax,
    Money Tip,
    Money Total)
{
    public static OrderDto From(Order order)
    {
        var totals = order.CalculateTotals();
        return new OrderDto(
            order.Id,
            order.Number,
            order.Status,
            order.CreatedAt,
            order.PaymentMethod,
            order.Lines.Select(OrderLineDto.From).ToList(),
            totals.Subtotal,
            totals.Discount,
            totals.Surcharge,
            totals.Tax,
            totals.Tip,
            totals.Total);
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

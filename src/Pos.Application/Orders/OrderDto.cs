using Pos.Domain.Common;
using Pos.Domain.Orders;

namespace Pos.Application.Orders;

public sealed record OrderDto(
    Guid Id,
    int Number,
    OrderStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt,
    DateTimeOffset? CancelledAt,
    PaymentMethod PaymentMethod,
    IReadOnlyList<OrderMenuItemDto> Items,
    Money Subtotal,
    Money Discount,
    Money Surcharge,
    Money Tax,
    Money Tip,
    Money Total)
{
    public static OrderDto From(Order order)
    {
        var totals = OrderPricing.CalculateTotals(order);
        return new OrderDto(
            order.Id,
            order.Number,
            order.Status,
            order.CreatedAt,
            order.PaidAt,
            order.CancelledAt,
            order.PaymentMethod,
            order.Items.Select(OrderMenuItemDto.From).ToList(),
            totals.Subtotal,
            totals.Discount,
            totals.Surcharge,
            totals.Tax,
            totals.Tip,
            totals.Total);
    }
}

public sealed record OrderMenuItemDto(
    Guid Id,
    Guid MenuItemId,
    string Name,
    Money UnitPrice,
    int Quantity,
    string? Notes,
    Money ItemTotal)
{
    public static OrderMenuItemDto From(OrderMenuItem item) =>
        new(item.Id, item.MenuItemId, item.Name, item.UnitPrice, item.Quantity, item.Notes, OrderPricing.CalculateItemTotal(item));
}

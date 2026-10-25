using Pos.Domain.Orders;

namespace Pos.Application.Orders;

public sealed record AddOrderItemRequest(Guid MenuItemId, int Quantity = 1);

public sealed record OrderItemDto(Guid Id, Guid MenuItemId, string Name, decimal UnitPrice, int Quantity, decimal LineTotal);

public sealed record OrderDto(Guid Id, DateTime CreatedAtUtc, string Status, IReadOnlyList<OrderItemDto> Items, decimal Total)
{
    public static OrderDto From(Order order) => new(
        order.Id,
        order.CreatedAtUtc,
        order.Status.ToString(),
        order.Items
            .Select(i => new OrderItemDto(i.Id, i.MenuItemId, i.Name, i.UnitPrice, i.Quantity, i.LineTotal))
            .ToList(),
        order.Total);
}

public sealed record OrderTotalDto(Guid OrderId, int ItemCount, decimal Total);

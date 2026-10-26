namespace Pos.Application.Orders.AddOrderItem;

public sealed record AddOrderItemCommand(Guid OrderId, Guid MenuItemId, int Quantity, string? Notes);

namespace Pos.Application.Orders.ChangeOrderMenuItemQuantity;

public sealed record ChangeOrderMenuItemQuantityCommand(Guid OrderId, Guid ItemId, int Quantity);

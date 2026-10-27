namespace Pos.Application.Orders.ChangeOrderLineQuantity;

public sealed record ChangeOrderLineQuantityCommand(Guid OrderId, Guid LineId, int Quantity);

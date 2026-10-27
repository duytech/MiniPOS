namespace Pos.Application.Orders.RemoveOrderLine;

public sealed record RemoveOrderLineCommand(Guid OrderId, Guid LineId);

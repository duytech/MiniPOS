namespace Pos.Application.Orders.RemoveOrderMenuItem;

public sealed record RemoveOrderMenuItemCommand(Guid OrderId, Guid ItemId);

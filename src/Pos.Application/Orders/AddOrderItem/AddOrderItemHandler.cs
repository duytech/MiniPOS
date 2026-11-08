using Pos.Application.Common;
using Pos.Application.MenuItems;
using Pos.Domain.Common;

namespace Pos.Application.Orders.AddOrderItem;

public sealed class AddOrderItemHandler(IOrderRepository orders, IMenuItemRepository menuItems, IUnitOfWork unitOfWork)
    : ICommandHandler<AddOrderItemCommand, OrderDto>
{
    public async Task<Result<OrderDto>> HandleAsync(AddOrderItemCommand command, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(command.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.Fail<OrderDto>(NotFoundErrors.Order(command.OrderId));
        }

        var menuItem = await menuItems.GetByIdAsync(command.MenuItemId, cancellationToken);
        if (menuItem is null)
        {
            return Result.Fail<OrderDto>(NotFoundErrors.MenuItem(command.MenuItemId));
        }

        var changed = order.AddItem(menuItem, command.Quantity, command.Notes);
        if (!changed.IsSuccess)
        {
            return Result.Fail<OrderDto>(changed.Error);
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? Result.Ok(OrderDto.From(order)) : Result.Fail<OrderDto>(saved.Error);
    }
}

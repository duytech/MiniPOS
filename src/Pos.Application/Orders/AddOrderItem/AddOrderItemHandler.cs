using Pos.Application.Common;
using Pos.Application.MenuItems;

namespace Pos.Application.Orders.AddOrderItem;

public sealed class AddOrderItemHandler(IOrderRepository orders, IMenuItemRepository menuItems, IUnitOfWork unitOfWork)
    : ICommandHandler<AddOrderItemCommand, OrderDto>
{
    public async Task<OrderDto> HandleAsync(AddOrderItemCommand command, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(command.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order", command.OrderId);
        var menuItem = await menuItems.GetByIdAsync(command.MenuItemId, cancellationToken)
            ?? throw new NotFoundException("Menu item", command.MenuItemId);

        order.AddItem(menuItem, command.Quantity, command.Notes);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return OrderDto.From(order);
    }
}

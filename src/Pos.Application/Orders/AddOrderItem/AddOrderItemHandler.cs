using Microsoft.Extensions.Logging;
using Pos.Application.Common;
using Pos.Application.MenuItems;
using Pos.Domain.Common;
using Pos.Domain.MenuItems;
using Pos.Domain.Orders;

namespace Pos.Application.Orders.AddOrderItem;

public sealed class AddOrderItemHandler(IOrderRepository orders, IMenuItemRepository menuItems,
    ILogger<AddOrderItemHandler> logger)
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

        if (!OrderRules.IsOpen(order))
        {
            return Result.Fail<OrderDto>(OrderErrors.NotOpen(order.Status));
        }

        if (AddItem(order, menuItem, command.Quantity, command.Notes) is { } error)
        {
            return Result.Fail<OrderDto>(error);
        }

        try
        {
            await orders.SaveAsync(cancellationToken);
        }
        catch (SaveFailedException ex)
        {
            logger.OrderSaveFailed(ex, order.Id, ex.Code);
            return Result.Fail<OrderDto>(ex.ToError());
        }

        return Result.Ok(OrderDto.From(order));
    }

    /// <summary>
    /// Adds the menu item at its current price. An item with the same menu item, snapshotted unit price and notes
    /// gets its quantity increased; anything different becomes a new item.
    /// </summary>
    private static Error? AddItem(Order order, MenuItem menuItem, int quantity, string? notes)
    {
        var normalizedNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        var item = order.Items.FirstOrDefault(l =>
            l.MenuItemId == menuItem.Id && l.UnitPrice == menuItem.Price && l.Notes == normalizedNotes);
        if (item is null)
        {
            order.Items.Add(new OrderMenuItem
            {
                Id = Guid.NewGuid(),
                MenuItemId = menuItem.Id,
                Name = menuItem.Name,
                UnitPrice = menuItem.Price,
                Quantity = quantity,
                Notes = normalizedNotes,
            });
        }
        else
        {
            try
            {
                item.Quantity = checked(item.Quantity + quantity);
            }
            catch (OverflowException)
            {
                return new Error(ErrorCode.ItemQuantityTooLarge, "The item quantity is too large.");
            }
        }

        order.SavedSplitParts.Clear();
        return null;
    }
}

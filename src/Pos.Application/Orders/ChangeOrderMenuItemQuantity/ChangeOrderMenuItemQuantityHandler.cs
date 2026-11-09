using Microsoft.Extensions.Logging;
using Pos.Application.Common;
using Pos.Domain.Common;
using Pos.Domain.Orders;

namespace Pos.Application.Orders.ChangeOrderMenuItemQuantity;

/// <summary>
/// Sets the item's quantity; the same quantity as now leaves the order unchanged. A percent discount and the tip
/// follow the new subtotal. A fixed discount must not be more than the new subtotal: the change is rejected and the
/// discount has to be changed or removed first.
/// </summary>
public sealed class ChangeOrderMenuItemQuantityHandler(IOrderRepository orders,
    ILogger<ChangeOrderMenuItemQuantityHandler> logger)
    : ICommandHandler<ChangeOrderMenuItemQuantityCommand, OrderDto>
{
    public async Task<Result<OrderDto>> HandleAsync(ChangeOrderMenuItemQuantityCommand command, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(command.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.Fail<OrderDto>(NotFoundErrors.Order(command.OrderId));
        }

        var item = order.Items.FirstOrDefault(l => l.Id == command.ItemId);
        if (item is null)
        {
            return Result.Fail<OrderDto>(NotFoundErrors.OrderMenuItem(command.ItemId));
        }

        if (!OrderRules.IsOpen(order))
        {
            return Result.Fail<OrderDto>(OrderErrors.NotOpen(order.Status));
        }

        if (ChangeQuantity(order, item, command.Quantity) is { } error)
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

    private static Error? ChangeQuantity(Order order, OrderMenuItem item, int quantity)
    {
        if (quantity == item.Quantity)
        {
            return null;
        }

        var newSubtotal = OrderPricing.SubtotalWithQuantity(order, item, quantity);
        if (OrderRules.FixedDiscountError(order.DiscountAmount, newSubtotal, $"A quantity of {quantity}") is { } tooLarge)
        {
            return tooLarge;
        }

        item.Quantity = quantity;
        order.SavedSplitParts.Clear();
        return null;
    }
}

using Microsoft.Extensions.Logging;
using Pos.Application.Common;
using Pos.Domain.Common;
using Pos.Domain.Orders;

namespace Pos.Application.Orders.RemoveOrderMenuItem;

/// <summary>
/// Removes the item from the order; removing the last item leaves the order open and empty. A percent discount
/// follows the new subtotal. A fixed discount must not be more than the new subtotal: the removal is rejected and
/// the discount has to be changed or removed first.
/// </summary>
public sealed class RemoveOrderMenuItemHandler(IOrderRepository orders, ILogger<RemoveOrderMenuItemHandler> logger)
    : ICommandHandler<RemoveOrderMenuItemCommand, OrderDto>
{
    public async Task<Result<OrderDto>> HandleAsync(RemoveOrderMenuItemCommand command, CancellationToken cancellationToken)
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

        var newSubtotal = OrderPricing.SubtotalWithQuantity(order, item, quantity: 0);
        if (order.DiscountAmount is { } discount && !OrderRules.FixedDiscountFits(discount, newSubtotal))
        {
            return Result.Fail<OrderDto>(OrderErrors.DiscountExceedsNewSubtotal("Removing the item", discount, newSubtotal));
        }

        order.Items.Remove(item);
        order.SavedSplitParts.Clear();

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
}

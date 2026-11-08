using Pos.Application.Common;
using Pos.Domain.Common;
using Pos.Domain.Orders;

namespace Pos.Application.Orders.ChangeOrderLineQuantity;

/// <summary>
/// Sets the line's quantity; the same quantity as now leaves the order unchanged. A percent discount and the tip
/// follow the new subtotal. A fixed discount must not be more than the new subtotal: the change is rejected and the
/// discount has to be changed or removed first.
/// </summary>
public sealed class ChangeOrderLineQuantityHandler(IOrderRepository orders)
    : ICommandHandler<ChangeOrderLineQuantityCommand, OrderDto>
{
    public async Task<Result<OrderDto>> HandleAsync(ChangeOrderLineQuantityCommand command, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(command.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.Fail<OrderDto>(NotFoundErrors.Order(command.OrderId));
        }

        var line = order.Lines.FirstOrDefault(l => l.Id == command.LineId);
        if (line is null)
        {
            return Result.Fail<OrderDto>(NotFoundErrors.OrderLine(command.LineId));
        }

        if ((OrderRules.OpenError(order) ?? ChangeQuantity(order, line, command.Quantity)) is { } error)
        {
            return Result.Fail<OrderDto>(error);
        }

        var saved = await orders.SaveAsync(cancellationToken);
        return saved.IsSuccess ? Result.Ok(OrderDto.From(order)) : Result.Fail<OrderDto>(saved.Error);
    }

    private static Error? ChangeQuantity(Order order, OrderLine line, int quantity)
    {
        if (quantity == line.Quantity)
        {
            return null;
        }

        var newSubtotal = OrderPricing.SubtotalWithQuantity(order, line, quantity);
        if (OrderRules.FixedDiscountError(order.DiscountAmount, newSubtotal, $"A quantity of {quantity}") is { } tooLarge)
        {
            return tooLarge;
        }

        line.Quantity = quantity;
        order.SavedSplitParts.Clear();
        return null;
    }
}

using Pos.Application.Common;
using Pos.Domain.Common;
using Pos.Domain.Orders;

namespace Pos.Application.Orders.RemoveOrderLine;

/// <summary>
/// Removes the line from the order; removing the last line leaves the order open and empty. A percent discount
/// follows the new subtotal. A fixed discount must not be more than the new subtotal: the removal is rejected and
/// the discount has to be changed or removed first.
/// </summary>
public sealed class RemoveOrderLineHandler(IOrderRepository orders, IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveOrderLineCommand, OrderDto>
{
    public async Task<Result<OrderDto>> HandleAsync(RemoveOrderLineCommand command, CancellationToken cancellationToken)
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

        var newSubtotal = OrderPricing.Subtotal(order) - OrderPricing.LineTotal(line);
        if ((OrderRules.OpenError(order)
             ?? OrderRules.FixedDiscountError(order.DiscountAmount, newSubtotal, "Removing the line")) is { } error)
        {
            return Result.Fail<OrderDto>(error);
        }

        order.Lines.Remove(line);
        order.SavedSplitParts.Clear();

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? Result.Ok(OrderDto.From(order)) : Result.Fail<OrderDto>(saved.Error);
    }
}

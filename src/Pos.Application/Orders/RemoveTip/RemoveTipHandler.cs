using Pos.Application.Common;
using Pos.Domain.Common;
using Pos.Domain.Orders;

namespace Pos.Application.Orders.RemoveTip;

public sealed class RemoveTipHandler(IOrderRepository orders)
    : ICommandHandler<RemoveTipCommand, OrderDto>
{
    public async Task<Result<OrderDto>> HandleAsync(RemoveTipCommand command, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(command.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.Fail<OrderDto>(NotFoundErrors.Order(command.OrderId));
        }

        if (OrderRules.OpenError(order) is { } notOpen)
        {
            return Result.Fail<OrderDto>(notOpen);
        }

        if (order.TipRate is not null)
        {
            order.TipRate = null;
            order.SavedSplitParts.Clear();
        }

        var saved = await orders.SaveAsync(cancellationToken);
        return saved.IsSuccess ? Result.Ok(OrderDto.From(order)) : Result.Fail<OrderDto>(saved.Error);
    }
}

using Pos.Application.Common;
using Pos.Domain.Common;
using Pos.Domain.Orders;

namespace Pos.Application.Orders.CancelOrder;

public sealed class CancelOrderHandler(IOrderRepository orders, TimeProvider timeProvider)
    : ICommandHandler<CancelOrderCommand, OrderDto>
{
    public async Task<Result<OrderDto>> HandleAsync(CancelOrderCommand command, CancellationToken cancellationToken)
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

        // A saved split is kept.
        order.Status = OrderStatus.Cancelled;
        order.CancelledAt = timeProvider.GetUtcNow().ToUniversalTime();

        var saved = await orders.SaveAsync(cancellationToken);
        return saved.IsSuccess ? Result.Ok(OrderDto.From(order)) : Result.Fail<OrderDto>(saved.Error);
    }
}

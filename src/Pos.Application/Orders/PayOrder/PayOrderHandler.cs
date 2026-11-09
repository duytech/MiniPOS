using Microsoft.Extensions.Logging;
using Pos.Application.Common;
using Pos.Domain.Common;
using Pos.Domain.Orders;

namespace Pos.Application.Orders.PayOrder;

public sealed class PayOrderHandler(IOrderRepository orders, TimeProvider timeProvider, ILogger<PayOrderHandler> logger)
    : ICommandHandler<PayOrderCommand, OrderDto>
{
    public async Task<Result<OrderDto>> HandleAsync(PayOrderCommand command, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(command.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.Fail<OrderDto>(NotFoundErrors.Order(command.OrderId));
        }

        if (!OrderRules.IsOpen(order))
        {
            return Result.Fail<OrderDto>(OrderErrors.NotOpen(order.Status));
        }

        // The totals are not changed, and a saved split is kept (display only).
        order.Status = OrderStatus.Paid;
        order.PaidAt = timeProvider.GetUtcNow().ToUniversalTime();

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

using Microsoft.Extensions.Logging;
using Pos.Application.Common;
using Pos.Domain.Common;
using Pos.Domain.Orders;

namespace Pos.Application.Orders.CancelOrder;

public sealed class CancelOrderHandler(IOrderRepository orders, TimeProvider timeProvider,
    ILogger<CancelOrderHandler> logger)
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

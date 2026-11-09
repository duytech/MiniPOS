using Microsoft.Extensions.Logging;
using Pos.Application.Common;
using Pos.Domain.Common;
using Pos.Domain.Orders;

namespace Pos.Application.Orders.RemoveDiscount;

public sealed class RemoveDiscountHandler(IOrderRepository orders, ILogger<RemoveDiscountHandler> logger)
    : ICommandHandler<RemoveDiscountCommand, OrderDto>
{
    public async Task<Result<OrderDto>> HandleAsync(RemoveDiscountCommand command, CancellationToken cancellationToken)
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

        if (order.DiscountRate is not null || order.DiscountAmount is not null)
        {
            order.DiscountRate = null;
            order.DiscountAmount = null;
            order.SavedSplitParts.Clear();
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
}

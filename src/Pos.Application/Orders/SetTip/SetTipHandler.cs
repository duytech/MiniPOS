using Microsoft.Extensions.Logging;
using Pos.Application.Common;
using Pos.Domain.Common;
using Pos.Domain.Orders;

namespace Pos.Application.Orders.SetTip;

public sealed class SetTipHandler(IOrderRepository orders, ILogger<SetTipHandler> logger)
    : ICommandHandler<SetTipCommand, OrderDto>
{
    public async Task<Result<OrderDto>> HandleAsync(SetTipCommand command, CancellationToken cancellationToken)
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

        if (order.TipRate != command.Rate)
        {
            order.TipRate = command.Rate;
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

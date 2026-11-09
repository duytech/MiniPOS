using Microsoft.Extensions.Logging;
using Pos.Application.Common;
using Pos.Domain.Common;
using Pos.Domain.Orders;

namespace Pos.Application.Orders.SetPaymentMethod;

public sealed class SetPaymentMethodHandler(IOrderRepository orders, ILogger<SetPaymentMethodHandler> logger)
    : ICommandHandler<SetPaymentMethodCommand, OrderDto>
{
    public async Task<Result<OrderDto>> HandleAsync(SetPaymentMethodCommand command, CancellationToken cancellationToken)
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

        if (order.PaymentMethod != command.PaymentMethod)
        {
            order.PaymentMethod = command.PaymentMethod;
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

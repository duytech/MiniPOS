using Microsoft.Extensions.Logging;
using Pos.Application.Common;
using Pos.Domain.Common;
using Pos.Domain.Orders;

namespace Pos.Application.Orders.ApplyDiscount;

/// <summary>Replaces any existing discount. The discount cannot exceed the subtotal at the time it is applied.</summary>
public sealed class ApplyDiscountHandler(IOrderRepository orders, ILogger<ApplyDiscountHandler> logger)
    : ICommandHandler<ApplyDiscountCommand, OrderDto>
{
    public async Task<Result<OrderDto>> HandleAsync(ApplyDiscountCommand command, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(command.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.Fail<OrderDto>(NotFoundErrors.Order(command.OrderId));
        }

        var discount = command.Rate is { } rate
            ? OrderDiscount.PercentOfSubtotal(rate)
            : OrderDiscount.FixedAmount(command.Amount!.Value);
        if ((OrderRules.OpenError(order) ?? DiscountError(order, discount)) is { } error)
        {
            return Result.Fail<OrderDto>(error);
        }

        if (order.DiscountRate != discount.Rate || order.DiscountAmount != discount.Amount)
        {
            order.DiscountRate = discount.Rate;
            order.DiscountAmount = discount.Amount;
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

    private static Error? DiscountError(Order order, OrderDiscount discount) => discount.Rate is { Value: > 100m } rate
        ? new Error(ErrorCode.DiscountExceedsSubtotal, $"A discount of {rate} is more than the subtotal.")
        : OrderRules.FixedDiscountError(discount.Amount, OrderPricing.Subtotal(order), change: null);
}

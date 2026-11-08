using Pos.Application.Common;
using Pos.Domain.Common;
using Pos.Domain.Orders;

namespace Pos.Application.Orders.ApplyDiscount;

public sealed class ApplyDiscountHandler(IOrderRepository orders, IUnitOfWork unitOfWork)
    : ICommandHandler<ApplyDiscountCommand, OrderDto>
{
    public async Task<Result<OrderDto>> HandleAsync(ApplyDiscountCommand command, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(command.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.Fail<OrderDto>(NotFoundErrors.Order(command.OrderId));
        }

        var changed = order.ApplyDiscount(command.Rate is { } rate
            ? OrderDiscount.PercentOfSubtotal(rate)
            : OrderDiscount.FixedAmount(command.Amount!.Value));
        if (!changed.IsSuccess)
        {
            return Result.Fail<OrderDto>(changed.Error);
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? Result.Ok(OrderDto.From(order)) : Result.Fail<OrderDto>(saved.Error);
    }
}

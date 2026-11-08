using Pos.Application.Common;
using Pos.Domain.Common;
using Pos.Domain.Orders;

namespace Pos.Application.Orders.RemoveDiscount;

public sealed class RemoveDiscountHandler(IOrderRepository orders, IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveDiscountCommand, OrderDto>
{
    public async Task<Result<OrderDto>> HandleAsync(RemoveDiscountCommand command, CancellationToken cancellationToken)
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

        if (order.DiscountRate is not null || order.DiscountAmount is not null)
        {
            order.DiscountRate = null;
            order.DiscountAmount = null;
            order.SavedSplitParts.Clear();
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? Result.Ok(OrderDto.From(order)) : Result.Fail<OrderDto>(saved.Error);
    }
}

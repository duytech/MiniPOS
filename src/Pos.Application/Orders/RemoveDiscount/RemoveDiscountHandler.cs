using Pos.Application.Common;
using Pos.Domain.Common;

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

        var changed = order.RemoveDiscount();
        if (!changed.IsSuccess)
        {
            return Result.Fail<OrderDto>(changed.Error);
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? Result.Ok(OrderDto.From(order)) : Result.Fail<OrderDto>(saved.Error);
    }
}

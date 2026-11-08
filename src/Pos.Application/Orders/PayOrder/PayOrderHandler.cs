using Pos.Application.Common;
using Pos.Domain.Common;

namespace Pos.Application.Orders.PayOrder;

public sealed class PayOrderHandler(IOrderRepository orders, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    : ICommandHandler<PayOrderCommand, OrderDto>
{
    public async Task<Result<OrderDto>> HandleAsync(PayOrderCommand command, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(command.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.Fail<OrderDto>(NotFoundErrors.Order(command.OrderId));
        }

        var changed = order.Pay(timeProvider.GetUtcNow());
        if (!changed.IsSuccess)
        {
            return Result.Fail<OrderDto>(changed.Error);
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? Result.Ok(OrderDto.From(order)) : Result.Fail<OrderDto>(saved.Error);
    }
}

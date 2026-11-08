using Pos.Application.Common;
using Pos.Domain.Common;
using Pos.Domain.Orders;

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

        if (OrderRules.OpenError(order) is { } notOpen)
        {
            return Result.Fail<OrderDto>(notOpen);
        }

        // The totals are not changed, and a saved split is kept (display only).
        order.Status = OrderStatus.Paid;
        order.PaidAt = timeProvider.GetUtcNow().ToUniversalTime();

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? Result.Ok(OrderDto.From(order)) : Result.Fail<OrderDto>(saved.Error);
    }
}

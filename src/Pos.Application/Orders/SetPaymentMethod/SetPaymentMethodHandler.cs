using Pos.Application.Common;
using Pos.Domain.Common;
using Pos.Domain.Orders;

namespace Pos.Application.Orders.SetPaymentMethod;

public sealed class SetPaymentMethodHandler(IOrderRepository orders, IUnitOfWork unitOfWork)
    : ICommandHandler<SetPaymentMethodCommand, OrderDto>
{
    public async Task<Result<OrderDto>> HandleAsync(SetPaymentMethodCommand command, CancellationToken cancellationToken)
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

        if (order.PaymentMethod != command.PaymentMethod)
        {
            order.PaymentMethod = command.PaymentMethod;
            order.SavedSplitParts.Clear();
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? Result.Ok(OrderDto.From(order)) : Result.Fail<OrderDto>(saved.Error);
    }
}

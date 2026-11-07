using Pos.Application.Common;
using Pos.Domain.Common;

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

        var changed = order.SetPaymentMethod(command.PaymentMethod);
        if (!changed.IsSuccess)
        {
            return Result.Fail<OrderDto>(changed.Error);
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? Result.Ok(OrderDto.From(order)) : Result.Fail<OrderDto>(saved.Error);
    }
}

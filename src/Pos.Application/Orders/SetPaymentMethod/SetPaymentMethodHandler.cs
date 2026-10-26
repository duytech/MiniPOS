using Pos.Application.Common;

namespace Pos.Application.Orders.SetPaymentMethod;

public sealed class SetPaymentMethodHandler(IOrderRepository orders, IUnitOfWork unitOfWork)
    : ICommandHandler<SetPaymentMethodCommand, OrderDto>
{
    public async Task<OrderDto> HandleAsync(SetPaymentMethodCommand command, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(command.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order", command.OrderId);

        order.SetPaymentMethod(command.PaymentMethod);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return OrderDto.From(order);
    }
}

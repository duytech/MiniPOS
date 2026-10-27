using Pos.Application.Common;

namespace Pos.Application.Orders.CancelOrder;

public sealed class CancelOrderHandler(IOrderRepository orders, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    : ICommandHandler<CancelOrderCommand, OrderDto>
{
    public async Task<OrderDto> HandleAsync(CancelOrderCommand command, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(command.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order", command.OrderId);

        order.Cancel(timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return OrderDto.From(order);
    }
}

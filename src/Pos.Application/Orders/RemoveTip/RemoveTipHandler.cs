using Pos.Application.Common;

namespace Pos.Application.Orders.RemoveTip;

public sealed class RemoveTipHandler(IOrderRepository orders, IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveTipCommand, OrderDto>
{
    public async Task<OrderDto> HandleAsync(RemoveTipCommand command, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(command.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order", command.OrderId);

        order.RemoveTip();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return OrderDto.From(order);
    }
}

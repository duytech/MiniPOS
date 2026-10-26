using Pos.Application.Common;

namespace Pos.Application.Orders.SetTip;

public sealed class SetTipHandler(IOrderRepository orders, IUnitOfWork unitOfWork)
    : ICommandHandler<SetTipCommand, OrderDto>
{
    public async Task<OrderDto> HandleAsync(SetTipCommand command, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(command.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order", command.OrderId);

        order.SetTip(command.Rate);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return OrderDto.From(order);
    }
}

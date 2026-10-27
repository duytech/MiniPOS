using Pos.Application.Common;

namespace Pos.Application.Orders.ChangeOrderLineQuantity;

public sealed class ChangeOrderLineQuantityHandler(IOrderRepository orders, IUnitOfWork unitOfWork)
    : ICommandHandler<ChangeOrderLineQuantityCommand, OrderDto>
{
    public async Task<OrderDto> HandleAsync(ChangeOrderLineQuantityCommand command, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(command.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order", command.OrderId);
        var line = order.Lines.FirstOrDefault(l => l.Id == command.LineId)
            ?? throw new NotFoundException("Order line", command.LineId);

        order.ChangeLineQuantity(line, command.Quantity);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return OrderDto.From(order);
    }
}

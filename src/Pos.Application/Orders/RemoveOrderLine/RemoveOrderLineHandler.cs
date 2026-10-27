using Pos.Application.Common;

namespace Pos.Application.Orders.RemoveOrderLine;

public sealed class RemoveOrderLineHandler(IOrderRepository orders, IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveOrderLineCommand, OrderDto>
{
    public async Task<OrderDto> HandleAsync(RemoveOrderLineCommand command, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(command.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order", command.OrderId);
        var line = order.Lines.FirstOrDefault(l => l.Id == command.LineId)
            ?? throw new NotFoundException("Order line", command.LineId);

        order.RemoveLine(line);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return OrderDto.From(order);
    }
}
